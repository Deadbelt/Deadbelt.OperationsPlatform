namespace Deadbelt.Infrastructure.Workspaces;

internal sealed class RecentWorkspaceSettings
{
    public List<RecentWorkspaceMetadata> RecentWorkspaces { get; init; } = [];
}