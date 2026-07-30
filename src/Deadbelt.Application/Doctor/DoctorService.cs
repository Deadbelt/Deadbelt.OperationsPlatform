using System.Diagnostics;
using Deadbelt.Domain.Doctor;
using Deadbelt.Domain.Environments;
using Microsoft.Extensions.Logging;

namespace Deadbelt.Application.Doctor;

public sealed class DoctorService : IDoctorService
{
    private readonly IReadOnlyList<IDoctorScanner> _scanners;
    private readonly ILogger<DoctorService> _logger;

    public DoctorService(
        IEnumerable<IDoctorScanner> scanners,
        ILogger<DoctorService> logger)
    {
        ArgumentNullException.ThrowIfNull(scanners);
        ArgumentNullException.ThrowIfNull(logger);

        _scanners = scanners.ToArray();
        _logger = logger;
    }

    public async Task<DoctorScanResult> ScanAsync(
        DoctorScanRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (request is null)
        {
            return DoctorScanResult.Failed(
                CreateFailure(
                    DoctorFindingCodes.InvalidRequest,
                    "Doctor scan request is invalid.",
                    "No scan request was supplied.",
                    "Select an Environment and provide its local server root before scanning."),
                stopwatch.Elapsed);
        }

        if (request.EnvironmentId.Value == Guid.Empty
            || string.IsNullOrWhiteSpace(request.WorkspaceId)
            || string.IsNullOrWhiteSpace(request.EnvironmentName)
            || string.IsNullOrWhiteSpace(request.TargetRootPath))
        {
            return DoctorScanResult.Failed(
                CreateFailure(
                    DoctorFindingCodes.InvalidRequest,
                    "Doctor scan request is incomplete.",
                    "The selected Environment or target root was not supplied.",
                    "Select an Environment and provide its local server root before scanning.",
                    request.TargetRootPath),
                stopwatch.Elapsed);
        }

        if (!Enum.IsDefined(request.GameType)
            || request.GameType == GameType.Unknown)
        {
            return DoctorScanResult.Failed(
                CreateFailure(
                    DoctorFindingCodes.UnsupportedGame,
                    "The selected game is not supported.",
                    $"The selected Environment uses game type '{request.GameType}'.",
                    "Select a DayZ Environment for the local Doctor scan."),
                stopwatch.Elapsed);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scanner = _scanners.FirstOrDefault(
                candidate => candidate.Supports(request.GameType));

            if (scanner is null)
            {
                return DoctorScanResult.Failed(
                    CreateFailure(
                        DoctorFindingCodes.UnsupportedGame,
                        "No Doctor scanner supports the selected game.",
                        $"No registered scanner supports game type '{request.GameType}'.",
                        "Select a DayZ Environment or install a scanner that supports this game type."),
                    stopwatch.Elapsed);
            }

            var result = await scanner.ScanAsync(
                request,
                cancellationToken);

            if (result is null)
            {
                _logger.LogError(
                    "Doctor scanner returned null for Workspace {WorkspaceId} and Environment {EnvironmentId}.",
                    request.WorkspaceId,
                    request.EnvironmentId);

                return DoctorScanResult.Failed(
                    CreateFailure(
                        DoctorFindingCodes.ScanFailed,
                        "Doctor scan could not be completed.",
                        "The scanner returned no usable result.",
                        "Run the scan again. If the problem persists, report the scanner failure.",
                        request.TargetRootPath),
                    stopwatch.Elapsed);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Doctor scan cancelled for Environment {EnvironmentId}.",
                request.EnvironmentId);

            return DoctorScanResult.Cancelled(stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Doctor scan failed for Environment {EnvironmentId}.",
                request.EnvironmentId);

            return DoctorScanResult.Failed(
                CreateFailure(
                    DoctorFindingCodes.ScanFailed,
                    "Doctor scan could not be completed.",
                    "The selected target could not be safely inspected.",
                    "Verify the selected paths are readable, then run the scan again.",
                    request.TargetRootPath),
                stopwatch.Elapsed);
        }
    }

    private static DoctorFinding CreateFailure(
        string code,
        string title,
        string evidence,
        string recommendation,
        string? sourcePath = null)
    {
        return new DoctorFinding(
            code,
            DoctorSeverity.Error,
            title,
            "The Doctor operation stopped without retaining partial scan state.",
            evidence,
            recommendation,
            sourcePath);
    }
}
