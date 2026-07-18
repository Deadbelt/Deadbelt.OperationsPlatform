namespace Deadbelt.Domain.Providers;

public enum ProviderType
{
    Unknown = 0,
    LocalWindows = 1,
    LocalLinux = 2,
    SteamCmd = 3,
    Rcon = 4,
    HostingProvider = 5,
    BackupProvider = 6,
    MonitoringProvider = 7,
    NotificationProvider = 8,
    Custom = 99
}
