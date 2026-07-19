using Deadbelt.Domain.Providers;

namespace Deadbelt.Desktop.ViewModels;

public sealed class ProviderTypeOptionViewModel
{
    private ProviderTypeOptionViewModel(
        ProviderType providerType,
        string displayName)
    {
        ProviderType = providerType;
        DisplayName = displayName;
    }

    public ProviderType ProviderType { get; }

    public string DisplayName { get; }

    public static IReadOnlyList<ProviderTypeOptionViewModel> CreateDefaultOptions()
    {
        return
        [
            new ProviderTypeOptionViewModel(ProviderType.LocalWindows, "Local Windows"),
            new ProviderTypeOptionViewModel(ProviderType.LocalLinux, "Local Linux"),
            new ProviderTypeOptionViewModel(ProviderType.SteamCmd, "SteamCMD"),
            new ProviderTypeOptionViewModel(ProviderType.Rcon, "RCON"),
            new ProviderTypeOptionViewModel(ProviderType.HostingProvider, "Hosting Provider"),
            new ProviderTypeOptionViewModel(ProviderType.BackupProvider, "Backup Provider"),
            new ProviderTypeOptionViewModel(ProviderType.MonitoringProvider, "Monitoring Provider"),
            new ProviderTypeOptionViewModel(ProviderType.NotificationProvider, "Notification Provider"),
            new ProviderTypeOptionViewModel(ProviderType.Custom, "Custom")
        ];
    }
}
