using Deadbelt.Domain.Providers;

namespace Deadbelt.Application.Providers;

public sealed class CreateProviderRequest
{
    public string WorkspacePath { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public ProviderType ProviderType { get; init; } = ProviderType.Unknown;
}
