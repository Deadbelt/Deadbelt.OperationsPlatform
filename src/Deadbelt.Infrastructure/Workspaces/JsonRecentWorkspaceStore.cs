using System.Text.Json;
using Deadbelt.Application.Workspaces;

namespace Deadbelt.Infrastructure.Workspaces;

public sealed class JsonRecentWorkspaceStore : IRecentWorkspaceStore
{
    private const string SettingsFolderName = "Deadbelt";
    private const string ProductFolderName = "OperationsPlatform";
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<RecentWorkspace>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var settingsFilePath = GetSettingsFilePath();

        if (!File.Exists(settingsFilePath))
            return Array.Empty<RecentWorkspace>();

        try
        {
            await using var stream = File.OpenRead(settingsFilePath);

            var settings = await JsonSerializer.DeserializeAsync<RecentWorkspaceSettings>(
                stream,
                JsonOptions,
                cancellationToken);

            if (settings is null)
                return Array.Empty<RecentWorkspace>();

            return settings.RecentWorkspaces
                .Where(metadata =>
                    !string.IsNullOrWhiteSpace(metadata.Name)
                    && !string.IsNullOrWhiteSpace(metadata.Path))
                .Select(metadata =>
                    new RecentWorkspace(
                        metadata.Name,
                        metadata.Path,
                        metadata.LastOpenedUtc))
                .OrderByDescending(workspace => workspace.LastOpenedUtc)
                .ToArray();
        }
        catch
        {
            return Array.Empty<RecentWorkspace>();
        }
    }

    public async Task SaveAsync(
        IReadOnlyList<RecentWorkspace> recentWorkspaces,
        CancellationToken cancellationToken = default)
    {
        var settingsFilePath = GetSettingsFilePath();
        var settingsFolderPath = Path.GetDirectoryName(settingsFilePath);

        if (string.IsNullOrWhiteSpace(settingsFolderPath))
            throw new InvalidOperationException("Unable to determine settings folder path.");

        Directory.CreateDirectory(settingsFolderPath);

        var settings = new RecentWorkspaceSettings
        {
            RecentWorkspaces = recentWorkspaces
                .Select(workspace =>
                    new RecentWorkspaceMetadata
                    {
                        Name = workspace.Name,
                        Path = workspace.Path,
                        LastOpenedUtc = workspace.LastOpenedUtc
                    })
                .ToList()
        };

        await using var stream = File.Create(settingsFilePath);

        await JsonSerializer.SerializeAsync(
            stream,
            settings,
            JsonOptions,
            cancellationToken);
    }

    private static string GetSettingsFilePath()
    {
        var appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);

        return Path.Combine(
            appDataPath,
            SettingsFolderName,
            ProductFolderName,
            SettingsFileName);
    }
}