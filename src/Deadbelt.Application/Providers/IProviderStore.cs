using Deadbelt.Domain.Providers;

namespace Deadbelt.Application.Providers;

public interface IProviderStore
{
    string GetProviderPath(
        string workspacePath,
        string providerName);

    Task<bool> ExistsAsync(
        string workspacePath,
        string providerName,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Provider provider,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Provider>> LoadByWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default);
}
