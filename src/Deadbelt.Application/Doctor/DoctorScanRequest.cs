using Deadbelt.Domain.Environments;

namespace Deadbelt.Application.Doctor;

public sealed class DoctorScanRequest
{
    public DoctorScanRequest(
        string workspaceId,
        EnvironmentId environmentId,
        string environmentName,
        GameType gameType,
        string targetRootPath,
        string? startupFilePath = null,
        string? configurationFilePath = null)
    {
        WorkspaceId = workspaceId;
        EnvironmentId = environmentId;
        EnvironmentName = environmentName;
        GameType = gameType;
        TargetRootPath = targetRootPath;
        StartupFilePath = startupFilePath;
        ConfigurationFilePath = configurationFilePath;
    }

    public string WorkspaceId { get; }

    public EnvironmentId EnvironmentId { get; }

    public string EnvironmentName { get; }

    public GameType GameType { get; }

    public string TargetRootPath { get; }

    public string? StartupFilePath { get; }

    public string? ConfigurationFilePath { get; }
}
