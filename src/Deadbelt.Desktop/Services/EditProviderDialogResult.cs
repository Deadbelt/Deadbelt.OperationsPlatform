using Deadbelt.Domain.Providers;

namespace Deadbelt.Desktop.Services;

public sealed class EditProviderDialogResult
{
    private EditProviderDialogResult(
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

    public static EditProviderDialogResult ConfirmedResult(
        string name,
        ProviderType providerType)
    {
        return new EditProviderDialogResult(
            true,
            name,
            providerType);
    }

    public static EditProviderDialogResult Cancelled()
    {
        return new EditProviderDialogResult(
            false,
            string.Empty,
            ProviderType.Unknown);
    }
}
