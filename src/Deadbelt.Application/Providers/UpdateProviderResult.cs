using Deadbelt.Domain.Providers;

namespace Deadbelt.Application.Providers;

public sealed class UpdateProviderResult
{
    private UpdateProviderResult(
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

    public static UpdateProviderResult Success(Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new UpdateProviderResult(
            true,
            provider,
            null);
    }

    public static UpdateProviderResult Failure(string errorMessage)
    {
        return new UpdateProviderResult(
            false,
            null,
            errorMessage);
    }
}
