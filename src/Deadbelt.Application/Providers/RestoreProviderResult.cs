using Deadbelt.Domain.Providers;

namespace Deadbelt.Application.Providers;

public sealed class RestoreProviderResult
{
    private RestoreProviderResult(
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

    public static RestoreProviderResult Success(Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new RestoreProviderResult(
            true,
            provider,
            null);
    }

    public static RestoreProviderResult Failure(string errorMessage)
    {
        return new RestoreProviderResult(
            false,
            null,
            errorMessage);
    }
}
