namespace Deadbelt.Application.Workspaces;

public interface IRecentWorkspaceStore
{
    Task<IReadOnlyList<RecentWorkspace>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyList<RecentWorkspace> recentWorkspaces,
        CancellationToken cancellationToken = default);
}