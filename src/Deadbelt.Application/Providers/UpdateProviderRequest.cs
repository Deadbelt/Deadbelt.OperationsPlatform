using Deadbelt.Domain.Providers;

namespace Deadbelt.Application.Providers;

public sealed class UpdateProviderRequest
{
    public string WorkspacePath { get; init; } = string.Empty;

    public Guid ProviderId { get; init; }

    public string Name { get; init; } = string.Empty;

    public ProviderType ProviderType { get; init; } = ProviderType.Unknown;
}
