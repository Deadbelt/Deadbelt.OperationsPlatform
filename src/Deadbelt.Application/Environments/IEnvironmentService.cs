using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Application.Environments;

public interface IEnvironmentService
{
    Task<CreateEnvironmentResult> CreateEnvironmentAsync(
        CreateEnvironmentRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateEnvironmentResult> UpdateEnvironmentAsync(
        UpdateEnvironmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ArchiveEnvironmentResult> ArchiveEnvironmentAsync(
        ArchiveEnvironmentRequest request,
        CancellationToken cancellationToken = default);

    Task<RestoreEnvironmentResult> RestoreEnvironmentAsync(
    RestoreEnvironmentRequest request,
    CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DOPEnvironment>> LoadByWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default);
}
