namespace Deadbelt.Infrastructure.Workspaces;

internal sealed class RecentWorkspaceMetadata
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public required DateTime LastOpenedUtc { get; init; }
}