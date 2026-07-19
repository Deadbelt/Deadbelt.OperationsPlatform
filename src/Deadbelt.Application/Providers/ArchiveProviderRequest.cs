namespace Deadbelt.Application.Providers;

public sealed class ArchiveProviderRequest
{
    public string WorkspacePath { get; init; } = string.Empty;

    public Guid ProviderId { get; init; }
}
