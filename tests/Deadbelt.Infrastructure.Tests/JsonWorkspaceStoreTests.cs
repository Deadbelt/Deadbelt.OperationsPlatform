using System.Text.Json;
using Deadbelt.Domain.Workspaces;
using Deadbelt.Infrastructure.Tests.TestSupport;
using Deadbelt.Infrastructure.Workspaces;

namespace Deadbelt.Infrastructure.Tests;

public sealed class JsonWorkspaceStoreTests
{
    [Fact]
    public async Task SaveAndLoadRoundTripPreservesSchemaValues()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var workspacePath = temporaryDirectory.GetPath("workspace");
        var createdUtc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var workspace = new Workspace(
            "Operations",
            workspacePath,
            "Test workspace",
            createdUtc,
            "0.1");
        var store = new JsonWorkspaceStore();

        await store.SaveAsync(workspace);
        var loaded = await store.LoadAsync(workspacePath);

        Assert.NotNull(loaded);
        Assert.Equal(workspace.Name, loaded.Name);
        Assert.Equal(workspace.Path, loaded.Path);
        Assert.Equal(workspace.Description, loaded.Description);
        Assert.Equal(workspace.CreatedUtc, loaded.CreatedUtc);
        Assert.Equal(workspace.Version, loaded.Version);

        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(workspacePath, "workspace.json")));

        JsonContractAssertions.HasExactlyProperties(
            document.RootElement,
            "Name",
            "Description",
            "CreatedUtc",
            "Version");

        Assert.Equal("Operations", document.RootElement.GetProperty("Name").GetString());
        Assert.Equal(
            "Test workspace",
            document.RootElement.GetProperty("Description").GetString());
        Assert.Equal(
            createdUtc,
            document.RootElement.GetProperty("CreatedUtc").GetDateTime());
        Assert.Equal("0.1", document.RootElement.GetProperty("Version").GetString());
    }

    [Fact]
    public async Task LoadReturnsNullWhenMetadataFileIsMissing()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new JsonWorkspaceStore();

        var loaded = await store.LoadAsync(temporaryDirectory.Path);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadRejectsIncompleteMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            temporaryDirectory.GetPath("workspace.json"),
            """{"Name":"Operations"}""");
        var store = new JsonWorkspaceStore();

        await Assert.ThrowsAsync<JsonException>(
            () => store.LoadAsync(temporaryDirectory.Path));
    }

    [Fact]
    public async Task LoadRejectsInvalidJson()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            temporaryDirectory.GetPath("workspace.json"),
            "{not-json");
        var store = new JsonWorkspaceStore();

        await Assert.ThrowsAsync<JsonException>(
            () => store.LoadAsync(temporaryDirectory.Path));
    }

    [Fact]
    public async Task SaveFailsWhenWorkspacePathIsAnExistingFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var blockedPath = temporaryDirectory.GetPath("blocked");
        await File.WriteAllTextAsync(blockedPath, "file");
        var workspace = new Workspace(
            "Operations",
            blockedPath,
            null,
            DateTime.UtcNow,
            "0.1");
        var store = new JsonWorkspaceStore();

        await Assert.ThrowsAnyAsync<IOException>(
            () => store.SaveAsync(workspace));
    }
}
