using Deadbelt.Domain.Environments;
using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Desktop.ViewModels;

public sealed class EnvironmentSummaryViewModel
{
    private EnvironmentSummaryViewModel(
        string id,
        string name,
        string description,
        GameType gameType,
        EnvironmentStatus status,
        string environmentPath,
        DateTime createdUtc,
        string version)
    {
        Id = id;
        Name = name;
        Description = description;
        GameType = gameType;
        Status = status;
        EnvironmentPath = environmentPath;
        CreatedUtc = createdUtc;
        Version = version;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public GameType GameType { get; }

    public EnvironmentStatus Status { get; }

    public string EnvironmentPath { get; }

    public DateTime CreatedUtc { get; }

    public string Version { get; }

    public string CreatedUtcDisplay => CreatedUtc.ToString("u");

    public bool IsArchived => Status == EnvironmentStatus.Archived;

    public string ArchivedStateMessage => IsArchived
        ? "This environment is archived. Files remain in place and metadata is still loadable."
        : string.Empty;

    public static EnvironmentSummaryViewModel FromEnvironment(DOPEnvironment environment)
    {
        return new EnvironmentSummaryViewModel(
            environment.Id.ToString(),
            environment.Name,
            environment.Description,
            environment.GameType,
            environment.Status,
            environment.EnvironmentPath,
            environment.CreatedUtc,
            environment.Version);
    }
}
