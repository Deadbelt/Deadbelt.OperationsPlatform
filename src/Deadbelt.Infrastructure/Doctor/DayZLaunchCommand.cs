namespace Deadbelt.Infrastructure.Doctor;

internal sealed class DayZLaunchCommand
{
    public DayZLaunchCommand(
        string executablePath,
        string? configurationPath,
        IEnumerable<string> clientModPaths,
        IEnumerable<string> serverModPaths,
        string? profilesPath,
        string? storagePath,
        string? mission = null,
        string? port = null,
        string? battleEyePath = null)
    {
        ExecutablePath = executablePath;
        ConfigurationPath = configurationPath;
        ClientModPaths = clientModPaths.ToArray();
        ServerModPaths = serverModPaths.ToArray();
        ProfilesPath = profilesPath;
        StoragePath = storagePath;
        Mission = mission;
        Port = port;
        BattleEyePath = battleEyePath;
    }

    public string ExecutablePath { get; }

    public string? ConfigurationPath { get; }

    public IReadOnlyList<string> ClientModPaths { get; }

    public IReadOnlyList<string> ServerModPaths { get; }

    public string? ProfilesPath { get; }

    public string? StoragePath { get; }

    public string? Mission { get; }

    public string? Port { get; }

    public string? BattleEyePath { get; }
}
