using Deadbelt.Domain.Providers;
using Microsoft.Extensions.Logging;

namespace Deadbelt.Application.Providers;

public sealed class ProviderService : IProviderService
{
    private readonly IProviderStore _providerStore;
    private readonly ILogger<ProviderService> _logger;

    public ProviderService(
        IProviderStore providerStore,
        ILogger<ProviderService> logger)
    {
        _providerStore = providerStore;
        _logger = logger;
    }

    public async Task<CreateProviderResult> CreateProviderAsync(
        CreateProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.WorkspacePath))
            return CreateProviderResult.Failure("Workspace path is required.");

        if (!Directory.Exists(request.WorkspacePath))
            return CreateProviderResult.Failure("Workspace path does not exist.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return CreateProviderResult.Failure("Provider name is required.");

        if (request.ProviderType == ProviderType.Unknown)
            return CreateProviderResult.Failure("Provider type is required.");

        var providerName = request.Name.Trim();

        try
        {
            var providerExists = await _providerStore.ExistsAsync(
                request.WorkspacePath,
                providerName,
                cancellationToken);

            if (providerExists)
            {
                return CreateProviderResult.Failure(
                    "A provider with this name already exists in the current workspace.");
            }

            var providerPath = _providerStore.GetProviderPath(
                request.WorkspacePath,
                providerName);

            var provider = Provider.Create(
                request.WorkspacePath,
                providerName,
                request.ProviderType,
                providerPath);

            await _providerStore.SaveAsync(
                provider,
                cancellationToken);

            _logger.LogInformation(
                "Created provider {ProviderName} at {ProviderPath}",
                provider.Name,
                provider.ProviderPath);

            return CreateProviderResult.Success(provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create provider {ProviderName}.",
                providerName);

            return CreateProviderResult.Failure(
                "Failed to create provider.");
        }
    }

    public async Task<IReadOnlyList<Provider>> LoadByWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            _logger.LogWarning("Unable to load providers because workspace path is empty.");
            return Array.Empty<Provider>();
        }

        if (!Directory.Exists(workspacePath))
        {
            _logger.LogWarning(
                "Unable to load providers because workspace path does not exist: {WorkspacePath}",
                workspacePath);

            return Array.Empty<Provider>();
        }

        try
        {
            var providers = await _providerStore.LoadByWorkspaceAsync(
                workspacePath,
                cancellationToken);

            _logger.LogInformation(
                "Loaded {ProviderCount} provider(s) from workspace {WorkspacePath}.",
                providers.Count,
                workspacePath);

            return providers;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load providers from workspace {WorkspacePath}.",
                workspacePath);

            return Array.Empty<Provider>();
        }
    }
}
