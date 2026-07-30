using Deadbelt.Application.Doctor;
using Deadbelt.Domain.Doctor;
using Deadbelt.Domain.Environments;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Application.Tests;

public sealed class DoctorServiceTests
{
    [Fact]
    public async Task NullRequestReturnsStableInvalidRequestFinding()
    {
        var scanner = new RecordingDoctorScanner();
        var service = CreateService(scanner);

        var result = await service.ScanAsync(null!);

        Assert.Equal(DoctorScanStatus.Failed, result.Status);
        Assert.Null(result.Inventory);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(DoctorFindingCodes.InvalidRequest, finding.Code);
        Assert.Equal(DoctorSeverity.Error, finding.Severity);
        Assert.False(scanner.WasCalled);
    }

    [Theory]
    [InlineData(null, "C:\\server")]
    [InlineData("", "C:\\server")]
    [InlineData("Environment", null)]
    [InlineData("Environment", "  ")]
    public async Task IncompleteRequestDoesNotInvokeScanner(
        string? environmentName,
        string? targetRoot)
    {
        var scanner = new RecordingDoctorScanner();
        var service = CreateService(scanner);
        var request = new DoctorScanRequest(
            "C:\\workspace",
            default,
            environmentName!,
            GameType.DayZ,
            targetRoot!);

        var result = await service.ScanAsync(request);

        Assert.Equal(DoctorScanStatus.Failed, result.Status);
        Assert.Null(result.Inventory);
        Assert.Equal(
            DoctorFindingCodes.InvalidRequest,
            Assert.Single(result.Findings).Code);
        Assert.False(scanner.WasCalled);
    }

    [Theory]
    [InlineData(GameType.Unknown)]
    [InlineData(GameType.Minecraft)]
    [InlineData((GameType)1234)]
    public async Task UnsupportedGameReturnsStableFinding(
        GameType gameType)
    {
        var scanner = new RecordingDoctorScanner();
        var service = CreateService(scanner);

        var result = await service.ScanAsync(
            CreateRequest(gameType));

        Assert.Equal(DoctorScanStatus.Failed, result.Status);
        Assert.Null(result.Inventory);
        Assert.Equal(
            DoctorFindingCodes.UnsupportedGame,
            Assert.Single(result.Findings).Code);
        Assert.False(scanner.WasCalled);
    }

    [Fact]
    public async Task SupportedRequestIsPassedToScanner()
    {
        var expected = DoctorScanResult.Completed(
            CreateInventory(),
            [],
            TimeSpan.FromMilliseconds(15));
        var scanner = new RecordingDoctorScanner(expected);
        var service = CreateService(scanner);
        var request = CreateRequest(GameType.DayZ);

        var result = await service.ScanAsync(request);

        Assert.Same(expected, result);
        Assert.Same(request, scanner.Request);
        Assert.True(scanner.WasCalled);
    }

    [Fact]
    public async Task CancellationReturnsCancelledWithoutErrorFinding()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var scanner = new RecordingDoctorScanner();
        var service = CreateService(scanner);

        var result = await service.ScanAsync(
            CreateRequest(GameType.DayZ),
            cancellation.Token);

        Assert.Equal(DoctorScanStatus.Cancelled, result.Status);
        Assert.Null(result.Inventory);
        Assert.Empty(result.Findings);
        Assert.False(scanner.WasCalled);
    }

    [Fact]
    public async Task ScannerCancellationIsNotConvertedToFailure()
    {
        using var cancellation = new CancellationTokenSource();
        var scanner = new ThrowingDoctorScanner(
            token =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(token);
            });
        var service = CreateService(scanner);

        var result = await service.ScanAsync(
            CreateRequest(GameType.DayZ),
            cancellation.Token);

        Assert.Equal(DoctorScanStatus.Cancelled, result.Status);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task UnexpectedScannerFailureReturnsSafeStableFinding()
    {
        const string sensitiveMessage = "private-key-content";
        var scanner = new ThrowingDoctorScanner(
            _ => throw new IOException(sensitiveMessage));
        var service = CreateService(scanner);

        var result = await service.ScanAsync(
            CreateRequest(GameType.DayZ));

        Assert.Equal(DoctorScanStatus.Failed, result.Status);
        Assert.Null(result.Inventory);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(DoctorFindingCodes.ScanFailed, finding.Code);
        Assert.Equal(DoctorSeverity.Error, finding.Severity);
        Assert.DoesNotContain(
            sensitiveMessage,
            string.Join(
                " ",
                finding.Title,
                finding.Explanation,
                finding.Evidence,
                finding.Recommendation),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScannerCapabilityFailureReturnsSafeStableFinding()
    {
        var service = CreateService(
            new ThrowingSupportsDoctorScanner());

        var result = await service.ScanAsync(
            CreateRequest(GameType.DayZ));

        Assert.Equal(DoctorScanStatus.Failed, result.Status);
        Assert.Equal(
            DoctorFindingCodes.ScanFailed,
            Assert.Single(result.Findings).Code);
    }

    [Fact]
    public async Task MissingWorkspaceIdentityDoesNotInvokeScanner()
    {
        var scanner = new RecordingDoctorScanner();
        var service = CreateService(scanner);
        var request = new DoctorScanRequest(
            " ",
            EnvironmentId.From(Guid.NewGuid()),
            "Environment",
            GameType.DayZ,
            "C:\\server");

        var result = await service.ScanAsync(request);

        Assert.Equal(DoctorScanStatus.Failed, result.Status);
        Assert.Equal(
            DoctorFindingCodes.InvalidRequest,
            Assert.Single(result.Findings).Code);
        Assert.False(scanner.WasCalled);
    }

    [Fact]
    public async Task NullScannerResultReturnsSafeFailure()
    {
        var service = CreateService(new NullReturningDoctorScanner());

        var result = await service.ScanAsync(CreateRequest(GameType.DayZ));

        Assert.Equal(DoctorScanStatus.Failed, result.Status);
        Assert.Null(result.Inventory);
        Assert.Equal(
            DoctorFindingCodes.ScanFailed,
            Assert.Single(result.Findings).Code);
    }

    [Fact]
    public async Task RequestPreservesWorkspaceAndEnvironmentProvenance()
    {
        var scanner = new RecordingDoctorScanner();
        var service = CreateService(scanner);
        var request = CreateRequest(GameType.DayZ);

        _ = await service.ScanAsync(request);

        Assert.Equal("C:\\workspace", scanner.Request!.WorkspaceId);
        Assert.Equal(request.EnvironmentId, scanner.Request.EnvironmentId);
    }

    private static DoctorService CreateService(
        IDoctorScanner scanner)
    {
        return new DoctorService(
            [scanner],
            NullLogger<DoctorService>.Instance);
    }

    private static DoctorScanRequest CreateRequest(GameType gameType)
    {
        return new DoctorScanRequest(
            "C:\\workspace",
            EnvironmentId.From(
                Guid.Parse("529b2628-ef27-49de-b4a3-b49c9d4fb058")),
            "Local DayZ",
            gameType,
            "C:\\server");
    }

    private static DoctorInventory CreateInventory()
    {
        return new DoctorInventory(
            "C:\\server",
            null,
            [],
            null,
            [],
            null,
            null,
            null,
            null,
            [],
            [],
            [],
            [],
            [],
            [],
            []);
    }

    private sealed class RecordingDoctorScanner : IDoctorScanner
    {
        private readonly DoctorScanResult? _result;

        public RecordingDoctorScanner(DoctorScanResult? result = null)
        {
            _result = result;
        }

        public bool WasCalled { get; private set; }

        public DoctorScanRequest? Request { get; private set; }

        public bool Supports(GameType gameType)
        {
            return gameType == GameType.DayZ;
        }

        public Task<DoctorScanResult> ScanAsync(
            DoctorScanRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            Request = request;

            return Task.FromResult(
                _result
                ?? DoctorScanResult.Cancelled(TimeSpan.Zero));
        }
    }

    private sealed class ThrowingDoctorScanner : IDoctorScanner
    {
        private readonly Func<CancellationToken, DoctorScanResult> _action;

        public ThrowingDoctorScanner(
            Func<CancellationToken, DoctorScanResult> action)
        {
            _action = action;
        }

        public bool Supports(GameType gameType)
        {
            return gameType == GameType.DayZ;
        }

        public Task<DoctorScanResult> ScanAsync(
            DoctorScanRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_action(cancellationToken));
        }
    }

    private sealed class ThrowingSupportsDoctorScanner : IDoctorScanner
    {
        public bool Supports(GameType gameType)
        {
            throw new InvalidOperationException(
                "Capability inspection failed.");
        }

        public Task<DoctorScanResult> ScanAsync(
            DoctorScanRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NullReturningDoctorScanner : IDoctorScanner
    {
        public bool Supports(GameType gameType) => gameType == GameType.DayZ;

        public Task<DoctorScanResult> ScanAsync(
            DoctorScanRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DoctorScanResult>(null!);
    }
}
