using Deadbelt.Domain.Providers;
using DOPProvider = Deadbelt.Domain.Providers.Provider;

namespace Deadbelt.Desktop.ViewModels;

public sealed class ProviderSummaryViewModel
{
    private ProviderSummaryViewModel(
        string id,
        string name,
        ProviderType providerType,
        ProviderStatus status,
        string providerPath,
        string workspacePath,
        DateTime createdUtc,
        string version)
    {
        Id = id;
        Name = name;
        ProviderType = providerType;
        Status = status;
        ProviderPath = providerPath;
        WorkspacePath = workspacePath;
        CreatedUtc = createdUtc;
        Version = version;
    }

    public string Id { get; }

    public string Name { get; }

    public ProviderType ProviderType { get; }

    public ProviderStatus Status { get; }

    public string ProviderPath { get; }

    public string WorkspacePath { get; }

    public DateTime CreatedUtc { get; }

    public string Version { get; }

    public string ProviderTypeDisplay => ProviderType.ToString();

    public string StatusDisplay => Status.ToString();

    public bool IsArchived => Status == ProviderStatus.Archived;

    public string ArchivedStateMessage =>
        "This Provider is archived. It remains stored in the Workspace but should not be used by future active workflows until restored.";

    public string CreatedUtcDisplay => CreatedUtc.ToString("u");

    public static ProviderSummaryViewModel FromProvider(DOPProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new ProviderSummaryViewModel(
            provider.Id.ToString(),
            provider.Name,
            provider.ProviderType,
            provider.Status,
            provider.ProviderPath,
            provider.WorkspacePath,
            provider.CreatedUtc,
            provider.Version);
    }
}
