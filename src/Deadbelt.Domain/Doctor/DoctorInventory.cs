using System.Collections.ObjectModel;

namespace Deadbelt.Domain.Doctor;

public sealed class DoctorInventory
{
    public DoctorInventory(
        string targetRootPath,
        string? executablePath,
        IEnumerable<string>? startupCandidates,
        string? selectedStartupPath,
        IEnumerable<string>? configurationCandidates,
        string? activeConfigurationPath,
        IReadOnlyDictionary<string, string>? configurationValues,
        string? missionTemplate,
        string? missionPath,
        IEnumerable<string>? missionFiles,
        IEnumerable<DoctorModInventory>? clientMods,
        IEnumerable<DoctorModInventory>? serverMods,
        IEnumerable<string>? globalKeys,
        IEnumerable<string>? profilePaths,
        IEnumerable<string>? storagePaths,
        IEnumerable<DoctorLogInventory>? logFiles,
        IReadOnlyDictionary<string, string>? launchArguments = null)
    {
        if (string.IsNullOrWhiteSpace(targetRootPath))
            throw new ArgumentException("Doctor target root path is required.", nameof(targetRootPath));

        TargetRootPath = targetRootPath.Trim();
        ExecutablePath = Normalize(executablePath);
        StartupCandidates = Snapshot(startupCandidates, nameof(startupCandidates));
        SelectedStartupPath = Normalize(selectedStartupPath);
        ConfigurationCandidates = Snapshot(configurationCandidates, nameof(configurationCandidates));
        ActiveConfigurationPath = Normalize(activeConfigurationPath);
        ConfigurationValues = Snapshot(
            configurationValues,
            nameof(configurationValues));
        MissionTemplate = Normalize(missionTemplate);
        MissionPath = Normalize(missionPath);
        MissionFiles = Snapshot(missionFiles, nameof(missionFiles));
        ClientMods = Snapshot(clientMods, nameof(clientMods));
        ServerMods = Snapshot(serverMods, nameof(serverMods));
        GlobalKeys = Snapshot(globalKeys, nameof(globalKeys));
        ProfilePaths = Snapshot(profilePaths, nameof(profilePaths));
        StoragePaths = Snapshot(storagePaths, nameof(storagePaths));
        LogFiles = Snapshot(logFiles, nameof(logFiles));
        LaunchArguments = Snapshot(launchArguments, nameof(launchArguments));
    }

    public string TargetRootPath { get; }

    public string? ExecutablePath { get; }

    public IReadOnlyList<string> StartupCandidates { get; }

    public string? SelectedStartupPath { get; }

    public IReadOnlyList<string> ConfigurationCandidates { get; }

    public string? ActiveConfigurationPath { get; }

    public IReadOnlyDictionary<string, string> ConfigurationValues { get; }

    public string? MissionTemplate { get; }

    public string? MissionPath { get; }

    public IReadOnlyList<string> MissionFiles { get; }

    public IReadOnlyList<DoctorModInventory> ClientMods { get; }

    public IReadOnlyList<DoctorModInventory> ServerMods { get; }

    public IReadOnlyList<string> GlobalKeys { get; }

    public IReadOnlyList<string> ProfilePaths { get; }

    public IReadOnlyList<string> StoragePaths { get; }

    public IReadOnlyList<DoctorLogInventory> LogFiles { get; }

    public IReadOnlyDictionary<string, string> LaunchArguments { get; }

    private static IReadOnlyList<string> Snapshot(
        IEnumerable<string>? values,
        string parameterName)
    {
        var snapshot = values?.ToArray() ?? [];

        if (snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Collection elements cannot be null or blank.",
                parameterName);
        }

        return Array.AsReadOnly(snapshot.Select(value => value.Trim()).ToArray());
    }

    private static IReadOnlyList<T> Snapshot<T>(
        IEnumerable<T>? values,
        string parameterName)
        where T : class
    {
        var snapshot = values?.ToArray() ?? [];

        if (snapshot.Any(value => value is null))
        {
            throw new ArgumentException(
                "Collection elements cannot be null.",
                parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyDictionary<string, string> Snapshot(
        IReadOnlyDictionary<string, string>? values,
        string parameterName)
    {
        var snapshot = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var pair in values ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(pair.Key)
                || string.IsNullOrWhiteSpace(pair.Value))
            {
                throw new ArgumentException(
                    "Configuration keys and values cannot be null or blank.",
                    parameterName);
            }

            if (pair.Key.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Password values cannot be retained in Doctor inventory.",
                    parameterName);
            }

            snapshot.Add(pair.Key.Trim(), pair.Value.Trim());
        }

        return new ReadOnlyDictionary<string, string>(snapshot);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
