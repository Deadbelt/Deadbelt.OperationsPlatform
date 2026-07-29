using System.Text.Json;
using Deadbelt.Application.Persistence;
using Deadbelt.Domain.Workspaces;
using Deadbelt.Infrastructure.Tests.TestSupport;
using Deadbelt.Infrastructure.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;

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
        var loadResult = await store.LoadAsync(workspacePath);
        var loaded = Assert.IsType<Workspace>(loadResult.Value);

        Assert.Empty(loadResult.Diagnostics);
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

        var loadResult = await store.LoadAsync(temporaryDirectory.Path);

        Assert.Null(loadResult.Value);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.WorkspaceMetadataMissing,
            PersistenceDiagnosticSeverity.Error,
            PersistenceResourceCategory.Workspace,
            temporaryDirectory.GetPath("workspace.json"),
            "Required workspace metadata was not found");
    }

    [Fact]
    public async Task LoadRejectsIncompleteMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            temporaryDirectory.GetPath("workspace.json"),
            """{"Name":"Operations"}""");
        var store = new JsonWorkspaceStore();

        var loadResult = await store.LoadAsync(temporaryDirectory.Path);

        Assert.Null(loadResult.Value);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.WorkspaceMetadataInvalid,
            PersistenceDiagnosticSeverity.Error,
            PersistenceResourceCategory.Workspace,
            temporaryDirectory.GetPath("workspace.json"),
            "is invalid");
    }

    [Fact]
    public async Task LoadRejectsInvalidJson()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            temporaryDirectory.GetPath("workspace.json"),
            "{not-json");
        var store = new JsonWorkspaceStore();

        var loadResult = await store.LoadAsync(temporaryDirectory.Path);

        Assert.Null(loadResult.Value);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.WorkspaceMetadataInvalid,
            PersistenceDiagnosticSeverity.Error,
            PersistenceResourceCategory.Workspace,
            temporaryDirectory.GetPath("workspace.json"),
            "is invalid");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoadReturnsBlockingUnreadableDiagnosticForReadFailure(
        bool unauthorized)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var metadataPath = temporaryDirectory.GetPath("workspace.json");
        var exceptionMessage = unauthorized
            ? "Deterministic unauthorized Workspace read."
            : "Deterministic Workspace I/O failure.";
        var readOperations = new FaultInjectingPersistenceReadOperations();
        readOperations.FailOpen(
            metadataPath,
            unauthorized
                ? new UnauthorizedAccessException(exceptionMessage)
                : new IOException(exceptionMessage));
        var store = new JsonWorkspaceStore(
            NullLogger<JsonWorkspaceStore>.Instance,
            readOperations);

        var loadResult = await store.LoadAsync(temporaryDirectory.Path);

        Assert.Null(loadResult.Value);
        Assert.True(loadResult.HasBlockingErrors);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.WorkspaceMetadataUnreadable,
            PersistenceDiagnosticSeverity.Error,
            PersistenceResourceCategory.Workspace,
            metadataPath,
            "could not be read",
            exceptionMessage);
    }

    [Fact]
    public async Task LoadClassifiesUnexpectedOpenFailureAsUnreadable()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var metadataPath = temporaryDirectory.GetPath("workspace.json");
        const string exceptionMessage = "Deterministic unexpected inspection failure.";
        var readOperations = new FaultInjectingPersistenceReadOperations();
        readOperations.FailOpen(
            metadataPath,
            new InvalidOperationException(exceptionMessage));
        var store = new JsonWorkspaceStore(
            NullLogger<JsonWorkspaceStore>.Instance,
            readOperations);

        var loadResult = await store.LoadAsync(temporaryDirectory.Path);

        Assert.Null(loadResult.Value);
        PersistenceDiagnosticAssertions.Single(
            loadResult.Diagnostics,
            PersistenceDiagnosticCodes.WorkspaceMetadataUnreadable,
            PersistenceDiagnosticSeverity.Error,
            PersistenceResourceCategory.Workspace,
            metadataPath,
            "could not be read",
            exceptionMessage);
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
