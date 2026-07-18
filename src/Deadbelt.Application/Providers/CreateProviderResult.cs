using Deadbelt.Domain.Providers;

namespace Deadbelt.Application.Providers;

public sealed class CreateProviderResult
{
    private CreateProviderResult(
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

    public static CreateProviderResult Success(Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new CreateProviderResult(
            true,
            provider,
            null);
    }

    public static CreateProviderResult Failure(string errorMessage)
    {
        return new CreateProviderResult(
            false,
            null,
            errorMessage);
    }
}
