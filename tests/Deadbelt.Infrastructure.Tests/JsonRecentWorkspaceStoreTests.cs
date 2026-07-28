using System.Text.Json;
using Deadbelt.Application.Workspaces;
using Deadbelt.Infrastructure.Tests.TestSupport;
using Deadbelt.Infrastructure.Workspaces;

namespace Deadbelt.Infrastructure.Tests;

public sealed class JsonRecentWorkspaceStoreTests
{
    [Fact]
    public async Task SaveAndLoadRoundTripUsesInjectedTestPathAndSortsNewestFirst()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var settingsPath = temporaryDirectory.GetPath("settings", "settings.json");
        var store = new JsonRecentWorkspaceStore(settingsPath);
        var older = new RecentWorkspace(
            "Older",
            temporaryDirectory.GetPath("older"),
            new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
        var newer = new RecentWorkspace(
            "Newer",
            temporaryDirectory.GetPath("newer"),
            new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc));

        await store.SaveAsync([older, newer]);
        var loaded = await store.LoadAsync();

        Assert.Collection(
            loaded,
            workspace =>
            {
                Assert.Equal(newer.Name, workspace.Name);
                Assert.Equal(newer.Path, workspace.Path);
                Assert.Equal(newer.LastOpenedUtc, workspace.LastOpenedUtc);
            },
            workspace =>
            {
                Assert.Equal(older.Name, workspace.Name);
                Assert.Equal(older.Path, workspace.Path);
                Assert.Equal(older.LastOpenedUtc, workspace.LastOpenedUtc);
            });
        Assert.True(File.Exists(settingsPath));

        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(settingsPath));
        JsonContractAssertions.HasExactlyProperties(
            document.RootElement,
            "RecentWorkspaces");

        var entries = document.RootElement
            .GetProperty("RecentWorkspaces")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(2, entries.Length);
        JsonContractAssertions.HasExactlyProperties(
            entries[0],
            "Name",
            "Path",
            "LastOpenedUtc");
        JsonContractAssertions.HasExactlyProperties(
            entries[1],
            "Name",
            "Path",
            "LastOpenedUtc");

        Assert.Equal(older.Name, entries[0].GetProperty("Name").GetString());
        Assert.Equal(older.Path, entries[0].GetProperty("Path").GetString());
        Assert.Equal(
            older.LastOpenedUtc,
            entries[0].GetProperty("LastOpenedUtc").GetDateTime());
        Assert.Equal(newer.Name, entries[1].GetProperty("Name").GetString());
        Assert.Equal(newer.Path, entries[1].GetProperty("Path").GetString());
        Assert.Equal(
            newer.LastOpenedUtc,
            entries[1].GetProperty("LastOpenedUtc").GetDateTime());
    }

    [Fact]
    public async Task LoadReturnsEmptyWhenSettingsFileIsMissing()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new JsonRecentWorkspaceStore(
            temporaryDirectory.GetPath("settings", "settings.json"));

        var loaded = await store.LoadAsync();

        Assert.Empty(loaded);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{not-json")]
    public async Task LoadReturnsEmptyForIncompleteOrInvalidJson(string json)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var settingsPath = temporaryDirectory.GetPath("settings.json");
        await File.WriteAllTextAsync(settingsPath, json);
        var store = new JsonRecentWorkspaceStore(settingsPath);

        var loaded = await store.LoadAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadFiltersIncompleteHistoryEntries()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var settingsPath = temporaryDirectory.GetPath("settings.json");
        await File.WriteAllTextAsync(
            settingsPath,
            """
            {
              "RecentWorkspaces": [
                {
                  "Name": "",
                  "Path": "",
                  "LastOpenedUtc": "2026-07-01T12:00:00Z"
                }
              ]
            }
            """);
        var store = new JsonRecentWorkspaceStore(settingsPath);

        var loaded = await store.LoadAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task SaveFailsWhenSettingsParentIsAnExistingFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var blockedParent = temporaryDirectory.GetPath("blocked");
        await File.WriteAllTextAsync(blockedParent, "file");
        var store = new JsonRecentWorkspaceStore(
            Path.Combine(blockedParent, "settings.json"));

        await Assert.ThrowsAnyAsync<IOException>(
            () => store.SaveAsync(
                [
                    new RecentWorkspace(
                        "Operations",
                        temporaryDirectory.GetPath("workspace"),
                        DateTime.UtcNow)
                ]));
    }
}
