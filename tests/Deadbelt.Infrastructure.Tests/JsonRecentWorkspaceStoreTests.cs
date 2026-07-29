using System.Text.Json;
using Deadbelt.Application.Persistence;
using Deadbelt.Application.Workspaces;
using Deadbelt.Infrastructure.Tests.TestSupport;
using Deadbelt.Infrastructure.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;

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
        var loadResult = await store.LoadAsync();
        var loaded = loadResult.Value;

        Assert.Empty(loadResult.Diagnostics);
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

        var loadResult = await store.LoadAsync();

        Assert.Empty(loadResult.Value);
        Assert.Empty(loadResult.Diagnostics);
    }

    [Fact]
    public async Task LoadTreatsDocumentWithoutRecentWorkspacesAsEmptyHistory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var settingsPath = temporaryDirectory.GetPath("settings.json");
        await File.WriteAllTextAsync(settingsPath, "{}");
        var store = new JsonRecentWorkspaceStore(settingsPath);

        var loadResult = await store.LoadAsync();

        Assert.Empty(loadResult.Value);
        Assert.Empty(loadResult.Diagnostics);
    }

    [Fact]
    public async Task LoadReturnsDiagnosticForInvalidJson()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var settingsPath = temporaryDirectory.GetPath("settings.json");
        await File.WriteAllTextAsync(settingsPath, "{not-json");
        var store = new JsonRecentWorkspaceStore(settingsPath);

        var loadResult = await store.LoadAsync();

        Assert.Empty(loadResult.Value);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.RecentWorkspaceSettingsInvalid,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.RecentWorkspaces,
            settingsPath,
            "are invalid");
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

        var loadResult = await store.LoadAsync();

        Assert.Empty(loadResult.Value);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.RecentWorkspaceSettingsInvalid,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.RecentWorkspaces,
            settingsPath,
            "contain invalid entries");
    }

    [Fact]
    public async Task LoadReturnsValidHistoryEntryWithSingleInvalidEntryDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var settingsPath = temporaryDirectory.GetPath("settings.json");
        var workspacePathJson = JsonSerializer.Serialize(
            temporaryDirectory.GetPath("workspace"));
        await File.WriteAllTextAsync(
            settingsPath,
            $$"""
            {
              "RecentWorkspaces": [
                {
                  "Name": "Valid",
                  "Path": {{workspacePathJson}},
                  "LastOpenedUtc": "2026-07-01T12:00:00Z"
                },
                {
                  "Name": "",
                  "Path": "",
                  "LastOpenedUtc": "2026-07-01T12:00:00Z"
                }
              ]
            }
            """);
        var store = new JsonRecentWorkspaceStore(settingsPath);

        var loadResult = await store.LoadAsync();

        var workspace = Assert.Single(loadResult.Value);
        Assert.Equal("Valid", workspace.Name);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.RecentWorkspaceSettingsInvalid,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.RecentWorkspaces,
            settingsPath,
            "contain invalid entries");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoadReturnsUnreadableDiagnosticForReadFailure(
        bool unauthorized)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var settingsPath = temporaryDirectory.GetPath("settings.json");
        var exceptionMessage = unauthorized
            ? "Deterministic unauthorized settings read."
            : "Deterministic settings I/O failure.";
        var readOperations = new FaultInjectingPersistenceReadOperations();
        readOperations.FailOpen(
            settingsPath,
            unauthorized
                ? new UnauthorizedAccessException(exceptionMessage)
                : new IOException(exceptionMessage));
        var store = new JsonRecentWorkspaceStore(
            settingsPath,
            NullLogger<JsonRecentWorkspaceStore>.Instance,
            readOperations);

        var loadResult = await store.LoadAsync();

        Assert.Empty(loadResult.Value);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.RecentWorkspaceSettingsUnreadable,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.RecentWorkspaces,
            settingsPath,
            "could not be read",
            exceptionMessage);
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
