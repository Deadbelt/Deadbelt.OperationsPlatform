using Deadbelt.Domain.Workspaces;

namespace Deadbelt.Application.Workspaces;

public interface IRecentWorkspaceService
{
    Task<IReadOnlyList<RecentWorkspace>> GetRecentWorkspacesAsync(
        CancellationToken cancellationToken = default);

    Task RecordWorkspaceAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default);
}