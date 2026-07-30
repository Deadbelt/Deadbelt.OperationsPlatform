using Deadbelt.Application.Doctor;
using Deadbelt.Desktop.Services;
using Deadbelt.Desktop.ViewModels;
using Deadbelt.Domain.Doctor;
using Deadbelt.Domain.Environments;
using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Desktop.Tests;

public sealed class DoctorViewModelTests
{
    [Fact]
    public void WorkspaceContextPopulatesEnvironmentSelection()
    {
        var viewModel = CreateViewModel(new RecordingDoctorService());
        var dayZ = CreateEnvironment("DayZ", GameType.DayZ);
        var minecraft = CreateEnvironment("Minecraft", GameType.Minecraft);

        viewModel.UpdateContext(
            "C:\\workspaces\\one",
            [
                EnvironmentSummaryViewModel.FromEnvironment(dayZ),
                EnvironmentSummaryViewModel.FromEnvironment(minecraft)
            ]);

        Assert.Equal(2, viewModel.Environments.Count);
        Assert.Equal(dayZ.Id, viewModel.SelectedEnvironment!.Id);
        Assert.False(viewModel.ScanCommand.CanExecute(null));

        viewModel.TargetRootPath = "C:\\dayz";

        Assert.True(viewModel.ScanCommand.CanExecute(null));
    }

    [Fact]
    public async Task ScanPassesOperatorSelectionsAndDisplaysResult()
    {
        var finding = new DoctorFinding(
            "DOP.Doctor.Test",
            DoctorSeverity.Warning,
            "Test warning",
            "Safe explanation.",
            "Safe evidence.",
            "Take a concrete action.");
        var service = new RecordingDoctorService(
            DoctorScanResult.Completed(
                CreateInventory(),
                [finding],
                TimeSpan.FromMilliseconds(42)));
        var viewModel = CreateViewModel(service);
        viewModel.UpdateContext(
            "C:\\workspaces\\one",
            [CreateSummary("DayZ", GameType.DayZ)]);
        viewModel.TargetRootPath = " C:\\dayz ";
        viewModel.StartupFilePath = " C:\\dayz\\start.bat ";
        viewModel.ConfigurationFilePath = " C:\\dayz\\server.cfg ";

        await viewModel.ScanCommand.ExecuteAsync();

        Assert.NotNull(service.Request);
        Assert.Equal("C:\\workspaces\\one", service.Request.WorkspaceId);
        Assert.Equal(" C:\\dayz ", service.Request.TargetRootPath);
        Assert.Equal("C:\\dayz\\start.bat", service.Request.StartupFilePath);
        Assert.Equal("C:\\dayz\\server.cfg", service.Request.ConfigurationFilePath);
        Assert.Equal("Completed", viewModel.ScanStatus);
        Assert.Equal("42 ms", viewModel.Duration);
        Assert.Equal(1, viewModel.WarningCount);
        Assert.Single(viewModel.Findings);
        Assert.Contains("Client mods: 0", viewModel.InventorySummary);
    }

    [Fact]
    public async Task EnvironmentChangeClearsCompletedResult()
    {
        var service = new RecordingDoctorService(
            DoctorScanResult.Completed(
                CreateInventory(),
                [],
                TimeSpan.Zero));
        var viewModel = CreateViewModel(service);
        var first = CreateSummary("One", GameType.DayZ);
        var second = CreateSummary("Two", GameType.DayZ);
        viewModel.UpdateContext(
            "C:\\workspaces\\one",
            [first, second]);
        viewModel.TargetRootPath = "C:\\dayz";
        await viewModel.ScanCommand.ExecuteAsync();
        Assert.True(viewModel.HasResults);

        viewModel.SelectedEnvironment = viewModel.Environments[1];

        Assert.False(viewModel.HasResults);
        Assert.Empty(viewModel.Findings);
        Assert.Equal(0, viewModel.ErrorCount);
    }

    [Fact]
    public async Task WorkspaceChangeCancelsAndDiscardsStaleResult()
    {
        var service = new DeferredDoctorService();
        var viewModel = CreateViewModel(service);
        viewModel.UpdateContext(
            "C:\\workspaces\\one",
            [CreateSummary("One", GameType.DayZ)]);
        viewModel.TargetRootPath = "C:\\dayz-one";
        var scanTask = viewModel.ScanCommand.ExecuteAsync();
        await service.Started.Task;

        viewModel.UpdateContext(
            "C:\\workspaces\\two",
            [CreateSummary("Two", GameType.DayZ)]);
        service.Complete(
            DoctorScanResult.Completed(
                CreateInventory(),
                [],
                TimeSpan.FromSeconds(1)));
        await scanTask;

        Assert.True(service.ObservedCancellation);
        Assert.False(viewModel.HasResults);
        Assert.Equal(string.Empty, viewModel.TargetRootPath);
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public async Task CancelActionReturnsClearCancelledState()
    {
        var service = new CancellableDoctorService();
        var viewModel = CreateViewModel(service);
        viewModel.UpdateContext(
            "C:\\workspaces\\one",
            [CreateSummary("One", GameType.DayZ)]);
        viewModel.TargetRootPath = "C:\\dayz";
        var scanTask = viewModel.ScanCommand.ExecuteAsync();
        await service.Started.Task;

        Assert.True(viewModel.CancelCommand.CanExecute(null));
        viewModel.CancelCommand.Execute(null);
        await scanTask;

        Assert.Equal("Cancelled", viewModel.ScanStatus);
        Assert.Empty(viewModel.Findings);
        Assert.Equal(
            "Doctor scan cancelled. Partial state was discarded.",
            viewModel.StatusMessage);
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public void BrowseCommandsUseDialogSelectionsWithoutFilesystemLogic()
    {
        var dialogs = new RecordingPathDialogService
        {
            FolderResult = "C:\\selected-root",
            FileResult = "C:\\selected-root\\start.cmd"
        };
        var viewModel = new DoctorViewModel(
            new RecordingDoctorService(),
            dialogs);

        viewModel.BrowseTargetRootCommand.Execute(null);
        viewModel.BrowseStartupFileCommand.Execute(null);

        Assert.Equal("C:\\selected-root", viewModel.TargetRootPath);
        Assert.Equal(
            "C:\\selected-root\\start.cmd",
            viewModel.StartupFilePath);
        Assert.Equal("C:\\selected-root", dialogs.LastInitialPath);
        Assert.Contains("*.ps1", dialogs.LastFilter, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspaceAndContextChangesRefreshClearCommandState()
    {
        var viewModel = CreateViewModel(new RecordingDoctorService());
        var startupChanges = 0;
        var configurationChanges = 0;
        viewModel.ClearStartupFileCommand.CanExecuteChanged +=
            (_, _) => startupChanges++;
        viewModel.ClearConfigurationFileCommand.CanExecuteChanged +=
            (_, _) => configurationChanges++;
        viewModel.UpdateContext(
            "C:\\workspaces\\one",
            [CreateSummary("One", GameType.DayZ)]);
        viewModel.StartupFilePath = "C:\\dayz\\start.bat";
        viewModel.ConfigurationFilePath = "C:\\dayz\\server.cfg";

        Assert.True(viewModel.ClearStartupFileCommand.CanExecute(null));
        Assert.True(viewModel.ClearConfigurationFileCommand.CanExecute(null));

        viewModel.UpdateContext(
            "C:\\workspaces\\two",
            [CreateSummary("Two", GameType.DayZ)]);

        Assert.False(viewModel.ClearStartupFileCommand.CanExecute(null));
        Assert.False(viewModel.ClearConfigurationFileCommand.CanExecute(null));
        Assert.True(startupChanges >= 2);
        Assert.True(configurationChanges >= 2);

        viewModel.StartupFilePath = "C:\\dayz\\start.bat";
        viewModel.ConfigurationFilePath = "C:\\dayz\\server.cfg";
        viewModel.ClearContext();

        Assert.False(viewModel.ClearStartupFileCommand.CanExecute(null));
        Assert.False(viewModel.ClearConfigurationFileCommand.CanExecute(null));
    }

    private static DoctorViewModel CreateViewModel(IDoctorService service)
    {
        return new DoctorViewModel(
            service,
            new RecordingPathDialogService());
    }

    private static EnvironmentSummaryViewModel CreateSummary(
        string name,
        GameType gameType)
    {
        return EnvironmentSummaryViewModel.FromEnvironment(
            CreateEnvironment(name, gameType));
    }

    private static DOPEnvironment CreateEnvironment(
        string name,
        GameType gameType)
    {
        return new DOPEnvironment(
            EnvironmentId.New(),
            "C:\\workspaces\\one",
            name,
            null,
            gameType,
            $"C:\\metadata\\{name}",
            new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc),
            "0.1");
    }

    private static DoctorInventory CreateInventory()
    {
        return new DoctorInventory(
            "C:\\dayz",
            "C:\\dayz\\DayZServer_x64.exe",
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

    private sealed class RecordingDoctorService : IDoctorService
    {
        private readonly DoctorScanResult _result;

        public RecordingDoctorService(DoctorScanResult? result = null)
        {
            _result = result
                ?? DoctorScanResult.Cancelled(TimeSpan.Zero);
        }

        public DoctorScanRequest? Request { get; private set; }

        public Task<DoctorScanResult> ScanAsync(
            DoctorScanRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class DeferredDoctorService : IDoctorService
    {
        private readonly TaskCompletionSource<DoctorScanResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ObservedCancellation { get; private set; }

        public Task<DoctorScanResult> ScanAsync(
            DoctorScanRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.Register(
                () => ObservedCancellation = true);
            Started.SetResult();
            return _completion.Task;
        }

        public void Complete(DoctorScanResult result)
        {
            _completion.SetResult(result);
        }
    }

    private sealed class CancellableDoctorService : IDoctorService
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<DoctorScanResult> ScanAsync(
            DoctorScanRequest request,
            CancellationToken cancellationToken = default)
        {
            Started.SetResult();

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
                throw new InvalidOperationException(
                    "The cancellation test did not cancel.");
            }
            catch (OperationCanceledException)
            {
                return DoctorScanResult.Cancelled(TimeSpan.Zero);
            }
        }
    }

    private sealed class RecordingPathDialogService : IDoctorPathDialogService
    {
        public string? FolderResult { get; set; }

        public string? FileResult { get; set; }

        public string? LastInitialPath { get; private set; }

        public string? LastFilter { get; private set; }

        public string? SelectFolder(
            string title,
            string? initialPath = null)
        {
            LastInitialPath = initialPath;
            return FolderResult;
        }

        public string? SelectFile(
            string title,
            string filter,
            string? initialPath = null)
        {
            LastInitialPath = initialPath;
            LastFilter = filter;
            return FileResult;
        }
    }
}
