using Deadbelt.Application.Persistence;
using Deadbelt.Domain.Workspaces;

namespace Deadbelt.Application.Workspaces;

public interface IWorkspaceStore
{
    Task SaveAsync(Workspace workspace, CancellationToken cancellationToken = default);

    Task<PersistenceLoadResult<Workspace?>> LoadAsync(
        string folderPath,
        CancellationToken cancellationToken = default);
}
