using Deadbelt.Application.Common;
using Deadbelt.Application.Persistence;
using Deadbelt.Application.Providers;
using Deadbelt.Application.Tests.TestSupport;
using Deadbelt.Domain.Providers;
using Deadbelt.Infrastructure.FileSystem;
using Deadbelt.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Application.Tests;

public sealed class ProviderServiceTests
{
    [Fact]
    public async Task CreateEditLoadArchiveAndRestorePreserveIdentityAndStoragePath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();

        var created = await service.CreateProviderAsync(
            new CreateProviderRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                Name = "Local Host",
                ProviderType = ProviderType.LocalWindows
            });

        Assert.True(created.Succeeded);
        Assert.NotNull(created.Provider);
        var originalId = created.Provider.Id;
        var originalPath = created.Provider.ProviderPath;
        Assert.Equal(
            temporaryDirectory.GetPath("providers", "local-host"),
            originalPath);

        var updated = await service.UpdateProviderAsync(
            new UpdateProviderRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                ProviderId = originalId.Value,
                Name = "Renamed Host",
                ProviderType = ProviderType.LocalLinux
            });

        Assert.True(updated.Succeeded);
        Assert.NotNull(updated.Provider);
        Assert.Equal(originalId, updated.Provider.Id);
        Assert.Equal("Renamed Host", updated.Provider.Name);
        Assert.Equal(ProviderType.LocalLinux, updated.Provider.ProviderType);
        Assert.Equal(originalPath, updated.Provider.ProviderPath);

        var archived = await service.ArchiveProviderAsync(
            new ArchiveProviderRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                ProviderId = originalId.Value
            });

        Assert.True(archived.Succeeded);
        Assert.Equal(ProviderStatus.Archived, archived.Provider?.Status);
        Assert.Equal(originalPath, archived.Provider?.ProviderPath);

        var restored = await service.RestoreProviderAsync(
            new RestoreProviderRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                ProviderId = originalId.Value
            });

        Assert.True(restored.Succeeded);
        Assert.Equal(ProviderStatus.Draft, restored.Provider?.Status);
        Assert.Equal(originalPath, restored.Provider?.ProviderPath);

        var loaded = (await service.LoadByWorkspaceAsync(temporaryDirectory.Path)).Value;
        var provider = Assert.Single(loaded);
        Assert.Equal(originalId, provider.Id);
        Assert.Equal("Renamed Host", provider.Name);
        Assert.Equal(originalPath, provider.ProviderPath);
        Assert.Equal(ProviderStatus.Draft, provider.Status);
    }

    [Fact]
    public async Task CreateRejectsDuplicateNamesThatMapToTheSameSlug()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();

        var first = await CreateAsync(service, temporaryDirectory.Path, "Local Host");
        var duplicate = await CreateAsync(service, temporaryDirectory.Path, "Local_Host");

        Assert.True(first.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Null(duplicate.Provider);
        Assert.Equal(
            "A provider with this name already exists in the current workspace.",
            duplicate.ErrorMessage);

        var loaded = (await service.LoadByWorkspaceAsync(temporaryDirectory.Path)).Value;
        var persisted = Assert.Single(loaded);
        Assert.Equal(first.Provider!.Id, persisted.Id);
        Assert.Equal("Local Host", persisted.Name);
    }

    [Fact]
    public async Task UpdateRejectsDuplicateDisplayName()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();
        var first = await CreateAsync(service, temporaryDirectory.Path, "First");
        var second = await CreateAsync(service, temporaryDirectory.Path, "Second");

        var result = await service.UpdateProviderAsync(
            new UpdateProviderRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                ProviderId = second.Provider!.Id.Value,
                Name = first.Provider!.Name,
                ProviderType = ProviderType.LocalWindows
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Provider);
        Assert.Equal(
            "A provider with this name already exists in the current workspace.",
            result.ErrorMessage);

        var loaded = (await service.LoadByWorkspaceAsync(temporaryDirectory.Path)).Value;
        Assert.Collection(
            loaded,
            provider =>
            {
                Assert.Equal(first.Provider!.Id, provider.Id);
                Assert.Equal("First", provider.Name);
            },
            provider =>
            {
                Assert.Equal(second.Provider!.Id, provider.Id);
                Assert.Equal("Second", provider.Name);
            });
    }

    [Fact]
    public async Task InvalidLifecycleTransitionsReturnFailures()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();
        var created = await CreateAsync(service, temporaryDirectory.Path, "Lifecycle");
        var id = created.Provider!.Id.Value;

        var restoreDraft = await service.RestoreProviderAsync(
            new RestoreProviderRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                ProviderId = id
            });

        var firstArchive = await service.ArchiveProviderAsync(
            new ArchiveProviderRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                ProviderId = id
            });

        var secondArchive = await service.ArchiveProviderAsync(
            new ArchiveProviderRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                ProviderId = id
            });

        Assert.False(restoreDraft.Succeeded);
        Assert.Null(restoreDraft.Provider);
        Assert.Equal(
            "Only archived providers can be restored.",
            restoreDraft.ErrorMessage);
        Assert.True(firstArchive.Succeeded);
        Assert.NotNull(firstArchive.Provider);
        Assert.Equal(ProviderStatus.Archived, firstArchive.Provider.Status);
        Assert.False(secondArchive.Succeeded);
        Assert.Null(secondArchive.Provider);
        Assert.Equal(
            "Provider is already archived.",
            secondArchive.ErrorMessage);

        var loaded = (await service.LoadByWorkspaceAsync(temporaryDirectory.Path)).Value;
        var persisted = Assert.Single(loaded);
        Assert.Equal(ProviderStatus.Archived, persisted.Status);
        Assert.Equal(id, persisted.Id.Value);
    }

    [Fact]
    public async Task CanceledCreateIsCurrentlyReturnedAsFailure()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();

        var result = await service.CreateProviderAsync(
            new CreateProviderRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                Name = "Canceled",
                ProviderType = ProviderType.LocalWindows
            },
            new CancellationToken(canceled: true));

        Assert.False(result.Succeeded);
        Assert.Null(result.Provider);
        Assert.Equal("Failed to create provider.", result.ErrorMessage);
        Assert.Empty(
            (await service.LoadByWorkspaceAsync(temporaryDirectory.Path)).Value);
    }

    [Fact]
    public async Task LoadReturnsValidProviderAndCorruptSiblingDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();
        var created = await CreateAsync(
            service,
            temporaryDirectory.Path,
            "Valid");
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

        var loadResult = await service.LoadByWorkspaceAsync(temporaryDirectory.Path);

        var provider = Assert.Single(loadResult.Value);
        Assert.Equal(created.Provider!.Id, provider.Id);
        var diagnostic = Assert.Single(loadResult.Diagnostics);
        Assert.Equal(
            PersistenceDiagnosticCodes.ProviderMetadataInvalid,
            diagnostic.Code);
        Assert.Equal(
            PersistenceDiagnosticSeverity.Warning,
            diagnostic.Severity);
        Assert.Equal(
            PersistenceResourceCategory.Provider,
            diagnostic.ResourceCategory);
        Assert.Equal(invalidMetadataPath, diagnostic.SourcePath);
        Assert.Contains(
            "is invalid",
            diagnostic.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonException",
            diagnostic.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRejectsMissingWorkspaceWithoutCallingStore()
    {
        var pathInspector = new RecordingPathInspector
        {
            DirectoryExistsResult = false
        };
        var store = new RecordingProviderStore();
        var service = CreateService(store, pathInspector);
        const string workspacePath = "missing-workspace";

        var result = await service.CreateProviderAsync(
            new CreateProviderRequest
            {
                WorkspacePath = workspacePath,
                Name = "Local Host",
                ProviderType = ProviderType.LocalWindows
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Provider);
        Assert.Equal("Workspace path does not exist.", result.ErrorMessage);
        Assert.Equal([workspacePath], pathInspector.DirectoryPaths);
        Assert.Empty(pathInspector.FullyQualifiedFolderPaths);
        Assert.Equal(0, store.ExistsCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task CreateUsesStoreWhenWorkspaceDirectoryExists()
    {
        var pathInspector = new RecordingPathInspector
        {
            DirectoryExistsResult = true
        };
        var store = new RecordingProviderStore();
        var service = CreateService(store, pathInspector);
        const string workspacePath = "inspector-approved-workspace";

        var result = await service.CreateProviderAsync(
            new CreateProviderRequest
            {
                WorkspacePath = workspacePath,
                Name = "Local Host",
                ProviderType = ProviderType.LocalWindows
            });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Provider);
        Assert.Equal(workspacePath, result.Provider.WorkspacePath);
        Assert.Equal([workspacePath], pathInspector.DirectoryPaths);
        Assert.Empty(pathInspector.FullyQualifiedFolderPaths);
        Assert.Equal(1, store.ExistsCallCount);
        Assert.Equal(1, store.SaveCallCount);
    }

    [Fact]
    public async Task CreateDoesNotInspectPathWhenWorkspacePathIsMissing()
    {
        var pathInspector = new RecordingPathInspector
        {
            DirectoryExistsResult = true
        };
        var store = new RecordingProviderStore();
        var service = CreateService(store, pathInspector);

        var result = await service.CreateProviderAsync(
            new CreateProviderRequest
            {
                WorkspacePath = " ",
                Name = "Local Host",
                ProviderType = ProviderType.LocalWindows
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Provider);
        Assert.Equal("Workspace path is required.", result.ErrorMessage);
        Assert.Empty(pathInspector.DirectoryPaths);
        Assert.Empty(pathInspector.FullyQualifiedFolderPaths);
        Assert.Equal(0, store.ExistsCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task CreateTreatsInspectorExceptionAsMissingWorkspace()
    {
        var store = new RecordingProviderStore();
        var service = CreateService(store, new ThrowingPathInspector());

        var result = await service.CreateProviderAsync(
            new CreateProviderRequest
            {
                WorkspacePath = "inspector-failure",
                Name = "Local Host",
                ProviderType = ProviderType.LocalWindows
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Provider);
        Assert.Equal("Workspace path does not exist.", result.ErrorMessage);
        Assert.Equal(0, store.ExistsCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    private static ProviderService CreateService()
    {
        var store = new JsonProviderStore(
            NullLogger<JsonProviderStore>.Instance);

        return CreateService(
            store,
            new OperatingSystemPathInspector());
    }

    private static ProviderService CreateService(
        IProviderStore store,
        IPathInspector pathInspector)
    {
        return new ProviderService(
            store,
            pathInspector,
            NullLogger<ProviderService>.Instance);
    }

    private static Task<CreateProviderResult> CreateAsync(
        ProviderService service,
        string workspacePath,
        string name)
    {
        return service.CreateProviderAsync(
            new CreateProviderRequest
            {
                WorkspacePath = workspacePath,
                Name = name,
                ProviderType = ProviderType.LocalWindows
            });
    }

    private sealed class RecordingProviderStore : IProviderStore
    {
        public int ExistsCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public string GetProviderPath(string workspacePath, string providerName)
        {
            return $"{workspacePath}/providers/local-host";
        }

        public Task<bool> ExistsAsync(
            string workspacePath,
            string providerName,
            CancellationToken cancellationToken = default)
        {
            ExistsCallCount++;
            return Task.FromResult(false);
        }

        public Task SaveAsync(
            Provider provider,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            Provider provider,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<PersistenceLoadResult<IReadOnlyList<Provider>>> LoadByWorkspaceAsync(
            string workspacePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                PersistenceLoadResult<IReadOnlyList<Provider>>.Success([]));
        }
    }
}
