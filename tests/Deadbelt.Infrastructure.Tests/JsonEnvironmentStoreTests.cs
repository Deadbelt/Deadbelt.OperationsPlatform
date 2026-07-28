using System.Text.Json;
using Deadbelt.Domain.Environments;
using Deadbelt.Infrastructure.Environments;
using Deadbelt.Infrastructure.Tests.TestSupport;
using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Infrastructure.Tests;

public sealed class JsonEnvironmentStoreTests
{
    [Fact]
    public async Task SaveAndLoadRoundTripPreservesSchemaValues()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var environment = CreateEnvironment(
            temporaryDirectory.Path,
            temporaryDirectory.GetPath("environments", "production"));
        var store = new JsonEnvironmentStore();

        await store.SaveAsync(environment);
        var loaded = Assert.Single(
            await store.LoadByWorkspaceAsync(temporaryDirectory.Path));

        Assert.Equal(environment.Id, loaded.Id);
        Assert.Equal(environment.WorkspacePath, loaded.WorkspacePath);
        Assert.Equal(environment.Name, loaded.Name);
        Assert.Equal(environment.Description, loaded.Description);
        Assert.Equal(environment.GameType, loaded.GameType);
        Assert.Equal(environment.EnvironmentPath, loaded.EnvironmentPath);
        Assert.Equal(environment.CreatedUtc, loaded.CreatedUtc);
        Assert.Equal(environment.Version, loaded.Version);
        Assert.Equal(environment.Status, loaded.Status);

        var metadataPath = Path.Combine(
            environment.EnvironmentPath,
            "environment.json");
        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(metadataPath));

        JsonContractAssertions.HasExactlyProperties(
            document.RootElement,
            "Id",
            "Name",
            "Description",
            "GameType",
            "EnvironmentPath",
            "CreatedUtc",
            "Version",
            "Status");

        Assert.Equal(
            environment.Id.Value,
            document.RootElement.GetProperty("Id").GetGuid());
        Assert.Equal(
            environment.Name,
            document.RootElement.GetProperty("Name").GetString());
        Assert.Equal(
            environment.Description,
            document.RootElement.GetProperty("Description").GetString());
        Assert.Equal("DayZ", document.RootElement.GetProperty("GameType").GetString());
        Assert.Equal(
            environment.EnvironmentPath,
            document.RootElement.GetProperty("EnvironmentPath").GetString());
        Assert.Equal(
            environment.CreatedUtc,
            document.RootElement.GetProperty("CreatedUtc").GetDateTime());
        Assert.Equal(
            environment.Version,
            document.RootElement.GetProperty("Version").GetString());
        Assert.Equal("Active", document.RootElement.GetProperty("Status").GetString());
    }

    [Fact]
    public async Task UpdatePreservesStoragePathAfterDisplayNameChange()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var original = CreateEnvironment(
            temporaryDirectory.Path,
            temporaryDirectory.GetPath("environments", "original-name"));
        var store = new JsonEnvironmentStore();
        await store.SaveAsync(original);
        var renamed = CreateEnvironment(
            temporaryDirectory.Path,
            original.EnvironmentPath,
            id: original.Id,
            name: "Renamed Environment");

        await store.UpdateAsync(renamed);
        var loaded = Assert.Single(
            await store.LoadByWorkspaceAsync(temporaryDirectory.Path));

        Assert.Equal("Renamed Environment", loaded.Name);
        Assert.Equal(original.EnvironmentPath, loaded.EnvironmentPath);
        Assert.True(File.Exists(
            Path.Combine(original.EnvironmentPath, "environment.json")));
        Assert.False(Directory.Exists(
            temporaryDirectory.GetPath("environments", "renamed-environment")));
    }

    [Fact]
    public async Task LoadReturnsEmptyWhenEnvironmentDirectoryIsMissing()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new JsonEnvironmentStore();

        var loaded = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadSkipsDirectoryWithoutMetadataFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(
            temporaryDirectory.GetPath("environments", "missing"));
        var store = new JsonEnvironmentStore();

        var loaded = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);

        Assert.Empty(loaded);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{not-json")]
    public async Task LoadSilentlySkipsIncompleteOrInvalidMetadata(string json)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var environmentPath = temporaryDirectory.GetPath("environments", "invalid");
        Directory.CreateDirectory(environmentPath);
        await File.WriteAllTextAsync(
            Path.Combine(environmentPath, "environment.json"),
            json);
        var store = new JsonEnvironmentStore();

        var loaded = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task SaveFailsWhenEnvironmentPathIsAnExistingFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var blockedPath = temporaryDirectory.GetPath("blocked");
        await File.WriteAllTextAsync(blockedPath, "file");
        var environment = CreateEnvironment(
            temporaryDirectory.Path,
            blockedPath);
        var store = new JsonEnvironmentStore();

        await Assert.ThrowsAnyAsync<IOException>(
            () => store.SaveAsync(environment));
    }

    [Fact]
    public async Task ExistenceCheckHonorsPreCanceledToken()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new JsonEnvironmentStore();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.EnvironmentPathExistsAsync(
                temporaryDirectory.GetPath("environment"),
                new CancellationToken(canceled: true)));
    }

    private static DOPEnvironment CreateEnvironment(
        string workspacePath,
        string environmentPath,
        EnvironmentId? id = null,
        string name = "Production")
    {
        return new DOPEnvironment(
            id ?? EnvironmentId.New(),
            workspacePath,
            name,
            "Primary environment",
            GameType.DayZ,
            environmentPath,
            new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            "0.1",
            EnvironmentStatus.Active);
    }
}
