using System.Text.Json;
using Deadbelt.Application.Persistence;
using Deadbelt.Domain.Providers;
using Deadbelt.Infrastructure.Providers;
using Deadbelt.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Infrastructure.Tests;

public sealed class JsonProviderStoreTests
{
    [Fact]
    public async Task SaveAndLoadRoundTripPreservesSchemaValues()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = CreateStore();
        var providerPath = store.GetProviderPath(
            temporaryDirectory.Path,
            "Local Host");
        var provider = CreateProvider(
            temporaryDirectory.Path,
            providerPath);

        await store.SaveAsync(provider);
        var loadResult = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);
        var loaded = Assert.Single(loadResult.Value);

        Assert.Empty(loadResult.Diagnostics);
        Assert.Equal(provider.Id, loaded.Id);
        Assert.Equal(provider.WorkspacePath, loaded.WorkspacePath);
        Assert.Equal(provider.Name, loaded.Name);
        Assert.Equal(provider.ProviderType, loaded.ProviderType);
        Assert.Equal(provider.ProviderPath, loaded.ProviderPath);
        Assert.Equal(provider.Status, loaded.Status);
        Assert.Equal(provider.CreatedUtc, loaded.CreatedUtc);
        Assert.Equal(provider.Version, loaded.Version);

        var metadataPath = Path.Combine(provider.ProviderPath, "provider.json");
        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(metadataPath));

        JsonContractAssertions.HasExactlyProperties(
            document.RootElement,
            "id",
            "workspacePath",
            "name",
            "providerType",
            "providerPath",
            "createdUtc",
            "version",
            "status");

        Assert.Equal(
            provider.Id.Value,
            document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(
            provider.WorkspacePath,
            document.RootElement.GetProperty("workspacePath").GetString());
        Assert.Equal(
            provider.Name,
            document.RootElement.GetProperty("name").GetString());
        Assert.Equal(
            "LocalWindows",
            document.RootElement.GetProperty("providerType").GetString());
        Assert.Equal(
            provider.ProviderPath,
            document.RootElement.GetProperty("providerPath").GetString());
        Assert.Equal(
            provider.CreatedUtc,
            document.RootElement.GetProperty("createdUtc").GetDateTime());
        Assert.Equal(
            provider.Version,
            document.RootElement.GetProperty("version").GetString());
        Assert.Equal(
            "Configured",
            document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdatePreservesStoragePathAfterDisplayNameChange()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = CreateStore();
        var originalPath = store.GetProviderPath(
            temporaryDirectory.Path,
            "Original Name");
        var original = CreateProvider(
            temporaryDirectory.Path,
            originalPath);
        await store.SaveAsync(original);
        var renamed = CreateProvider(
            temporaryDirectory.Path,
            originalPath,
            id: original.Id,
            name: "Renamed Provider");

        await store.UpdateAsync(renamed);
        var loadResult = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);
        var loaded = Assert.Single(loadResult.Value);

        Assert.Equal("Renamed Provider", loaded.Name);
        Assert.Equal(originalPath, loaded.ProviderPath);
        Assert.True(File.Exists(Path.Combine(originalPath, "provider.json")));
        Assert.False(Directory.Exists(
            temporaryDirectory.GetPath("providers", "renamed-provider")));
    }

    [Fact]
    public async Task LoadReturnsEmptyWhenProviderDirectoryIsMissing()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = CreateStore();

        var loadResult = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);

        Assert.Empty(loadResult.Value);
        Assert.Empty(loadResult.Diagnostics);
    }

    [Fact]
    public async Task LoadSkipsDirectoryWithoutMetadataFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(
            temporaryDirectory.GetPath("providers", "missing"));
        var store = CreateStore();

        var loadResult = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);

        Assert.Empty(loadResult.Value);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.ProviderMetadataMissing,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.Provider,
            Path.Combine(
                temporaryDirectory.GetPath("providers", "missing"),
                "provider.json"),
            "was not found");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{not-json")]
    public async Task LoadSkipsIncompleteOrInvalidMetadata(string json)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var providerPath = temporaryDirectory.GetPath("providers", "invalid");
        Directory.CreateDirectory(providerPath);
        await File.WriteAllTextAsync(
            Path.Combine(providerPath, "provider.json"),
            json);
        var store = CreateStore();

        var loadResult = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);

        Assert.Empty(loadResult.Value);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.ProviderMetadataInvalid,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.Provider,
            Path.Combine(providerPath, "provider.json"),
            "is invalid");
    }

    [Fact]
    public async Task LoadReturnsValidSiblingAndExactlyOneCorruptChildDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = CreateStore();
        var validProviderPath = store.GetProviderPath(
            temporaryDirectory.Path,
            "Valid");
        var validProvider = CreateProvider(
            temporaryDirectory.Path,
            validProviderPath,
            name: "Valid");
        await store.SaveAsync(validProvider);
        var invalidProviderPath = temporaryDirectory.GetPath(
            "providers",
            "invalid");
        Directory.CreateDirectory(invalidProviderPath);
        var invalidMetadataPath = Path.Combine(
            invalidProviderPath,
            "provider.json");
        await File.WriteAllTextAsync(
            invalidMetadataPath,
            "{not-json");

        var loadResult = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);

        var loaded = Assert.Single(loadResult.Value);
        Assert.Equal(validProvider.Id, loaded.Id);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.ProviderMetadataInvalid,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.Provider,
            invalidMetadataPath,
            "is invalid");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoadReturnsCollectionUnreadableDiagnosticForEnumerationFailure(
        bool unauthorized)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var providersPath = temporaryDirectory.GetPath("providers");
        var exceptionMessage = unauthorized
            ? "Deterministic unauthorized Provider enumeration."
            : "Deterministic Provider enumeration I/O failure.";
        var readOperations = new FaultInjectingPersistenceReadOperations();
        readOperations.FailEnumeration(
            providersPath,
            unauthorized
                ? new UnauthorizedAccessException(exceptionMessage)
                : new IOException(exceptionMessage));
        var store = new JsonProviderStore(
            NullLogger<JsonProviderStore>.Instance,
            readOperations);

        var loadResult = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);

        Assert.Empty(loadResult.Value);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.ProviderCollectionUnreadable,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.Provider,
            providersPath,
            "could not be enumerated",
            exceptionMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoadKeepsValidSiblingWhenChildIsUnreadable(
        bool unauthorized)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var readOperations = new FaultInjectingPersistenceReadOperations();
        var store = new JsonProviderStore(
            NullLogger<JsonProviderStore>.Instance,
            readOperations);
        var validProviderPath = store.GetProviderPath(
            temporaryDirectory.Path,
            "Valid");
        var validProvider = CreateProvider(
            temporaryDirectory.Path,
            validProviderPath,
            name: "Valid");
        await store.SaveAsync(validProvider);
        var unreadableProviderPath = temporaryDirectory.GetPath(
            "providers",
            "unreadable");
        Directory.CreateDirectory(unreadableProviderPath);
        var unreadableMetadataPath = Path.Combine(
            unreadableProviderPath,
            "provider.json");
        var exceptionMessage = unauthorized
            ? "Deterministic unauthorized Provider child read."
            : "Deterministic Provider child I/O failure.";
        readOperations.FailOpen(
            unreadableMetadataPath,
            unauthorized
                ? new UnauthorizedAccessException(exceptionMessage)
                : new IOException(exceptionMessage));

        var loadResult = await store.LoadByWorkspaceAsync(temporaryDirectory.Path);

        var loaded = Assert.Single(loadResult.Value);
        Assert.Equal(validProvider.Id, loaded.Id);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.ProviderMetadataUnreadable,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.Provider,
            unreadableMetadataPath,
            "could not be read",
            exceptionMessage);
    }

    [Fact]
    public async Task SaveFailsWhenProviderPathIsAnExistingFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var blockedPath = temporaryDirectory.GetPath("blocked");
        await File.WriteAllTextAsync(blockedPath, "file");
        var provider = CreateProvider(
            temporaryDirectory.Path,
            blockedPath);
        var store = CreateStore();

        await Assert.ThrowsAnyAsync<IOException>(
            () => store.SaveAsync(provider));
    }

    [Fact]
    public async Task ExistenceCheckHonorsPreCanceledToken()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = CreateStore();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.ExistsAsync(
                temporaryDirectory.Path,
                "Local Host",
                new CancellationToken(canceled: true)));
    }

    private static JsonProviderStore CreateStore()
    {
        return new JsonProviderStore(
            NullLogger<JsonProviderStore>.Instance);
    }

    private static Provider CreateProvider(
        string workspacePath,
        string providerPath,
        ProviderId? id = null,
        string name = "Local Host")
    {
        return new Provider(
            id ?? ProviderId.New(),
            workspacePath,
            name,
            ProviderType.LocalWindows,
            providerPath,
            ProviderStatus.Configured,
            new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            "0.1");
    }
}
