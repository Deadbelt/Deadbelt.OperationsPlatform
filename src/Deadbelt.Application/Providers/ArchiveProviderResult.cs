using Deadbelt.Domain.Providers;

namespace Deadbelt.Application.Providers;

public sealed class ArchiveProviderResult
{
    private ArchiveProviderResult(
        bool succeeded,
        Provider? provider,
        string? errorMessage)
    {
        Succeeded = succeeded;
        Provider = provider;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public Provider? Provider { get; }

    public string? ErrorMessage { get; }

    public static ArchiveProviderResult Success(Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new ArchiveProviderResult(
            true,
            provider,
            null);
    }

    public static ArchiveProviderResult Failure(string errorMessage)
    {
        return new ArchiveProviderResult(
            false,
            null,
            errorMessage);
    }
}
