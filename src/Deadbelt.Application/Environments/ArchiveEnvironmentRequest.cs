namespace Deadbelt.Application.Environments;

public sealed class ArchiveEnvironmentRequest
{
    public required string WorkspacePath { get; init; }

    public required Guid EnvironmentId { get; init; }
}