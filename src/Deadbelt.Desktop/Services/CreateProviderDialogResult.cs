using Deadbelt.Domain.Providers;

namespace Deadbelt.Desktop.Services;

public sealed class CreateProviderDialogResult
{
    private CreateProviderDialogResult(
        bool confirmed,
        string name,
        ProviderType providerType)
    {
        Confirmed = confirmed;
        Name = name;
        ProviderType = providerType;
    }

    public bool Confirmed { get; }

    public string Name { get; }

    public ProviderType ProviderType { get; }

    public static CreateProviderDialogResult ConfirmedResult(
        string name,
        ProviderType providerType)
    {
        return new CreateProviderDialogResult(
            true,
            name,
            providerType);
    }

    public static CreateProviderDialogResult Cancelled()
    {
        return new CreateProviderDialogResult(
            false,
            string.Empty,
            ProviderType.Unknown);
    }
}
