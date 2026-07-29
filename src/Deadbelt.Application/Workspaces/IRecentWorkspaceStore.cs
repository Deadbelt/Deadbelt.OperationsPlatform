using Deadbelt.Application.Persistence;

namespace Deadbelt.Application.Workspaces;

public interface IRecentWorkspaceStore
{
    Task<PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyList<RecentWorkspace> recentWorkspaces,
        CancellationToken cancellationToken = default);
}
