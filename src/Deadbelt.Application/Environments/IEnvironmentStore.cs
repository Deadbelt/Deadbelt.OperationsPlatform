using Deadbelt.Application.Persistence;
using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Application.Environments;

public interface IEnvironmentStore
{
    Task SaveAsync(
        DOPEnvironment environment,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        DOPEnvironment environment,
        CancellationToken cancellationToken = default);

    Task<PersistenceLoadResult<IReadOnlyList<DOPEnvironment>>> LoadByWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default);

    Task<bool> EnvironmentPathExistsAsync(
        string environmentPath,
        CancellationToken cancellationToken = default);
}
