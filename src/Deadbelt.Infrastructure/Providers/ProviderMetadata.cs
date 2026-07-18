using Deadbelt.Domain.Providers;

namespace Deadbelt.Infrastructure.Providers;

public sealed class ProviderMetadata
{
    public Guid Id { get; init; }

    public string WorkspacePath { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public ProviderType ProviderType { get; init; } = ProviderType.Unknown;

    public string ProviderPath { get; init; } = string.Empty;

    public DateTime CreatedUtc { get; init; }

    public string Version { get; init; } = Provider.CurrentVersion;

    public ProviderStatus Status { get; init; } = ProviderStatus.Unknown;

    public static ProviderMetadata FromProvider(Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new ProviderMetadata
        {
            Id = provider.Id.Value,
            WorkspacePath = provider.WorkspacePath,
            Name = provider.Name,
            ProviderType = provider.ProviderType,
            ProviderPath = provider.ProviderPath,
            CreatedUtc = provider.CreatedUtc,
            Version = provider.Version,
            Status = provider.Status
        };
    }

    public Provider ToProvider()
    {
        return new Provider(
            ProviderId.From(Id),
            WorkspacePath,
            Name,
            ProviderType,
            ProviderPath,
            Status,
            CreatedUtc,
            Version);
    }
}
