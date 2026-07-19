using Deadbelt.Domain.Providers;

namespace Deadbelt.Application.Providers;

public interface IProviderService
{
    Task<CreateProviderResult> CreateProviderAsync(
        CreateProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateProviderResult> UpdateProviderAsync(
        UpdateProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<ArchiveProviderResult> ArchiveProviderAsync(
        ArchiveProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<RestoreProviderResult> RestoreProviderAsync(
        RestoreProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Provider>> LoadByWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default);
}
