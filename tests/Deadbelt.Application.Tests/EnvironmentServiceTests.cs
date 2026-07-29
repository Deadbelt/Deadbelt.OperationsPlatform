using Deadbelt.Application.Common;
using Deadbelt.Application.Environments;
using Deadbelt.Application.Tests.TestSupport;
using Deadbelt.Domain.Environments;
using Deadbelt.Infrastructure.Environments;
using Deadbelt.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Application.Tests;

public sealed class EnvironmentServiceTests
{
    [Fact]
    public async Task CreateEditLoadArchiveAndRestorePreserveIdentityAndStoragePath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();

        var created = await service.CreateEnvironmentAsync(
            new CreateEnvironmentRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                Name = "Primary Server",
                Description = "Initial",
                GameType = GameType.DayZ
            });

        Assert.True(created.Succeeded);
        Assert.NotNull(created.Environment);
        var originalId = created.Environment.Id;
        var originalPath = created.Environment.EnvironmentPath;
        Assert.Equal(
            temporaryDirectory.GetPath("environments", "primary-server"),
            originalPath);

        var updated = await service.UpdateEnvironmentAsync(
            new UpdateEnvironmentRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                EnvironmentId = originalId.Value,
                Name = "Renamed Server",
                Description = "Updated",
                GameType = GameType.Rust
            });

        Assert.True(updated.Succeeded);
        Assert.NotNull(updated.Environment);
        Assert.Equal(originalId, updated.Environment.Id);
        Assert.Equal("Renamed Server", updated.Environment.Name);
        Assert.Equal("Updated", updated.Environment.Description);
        Assert.Equal(GameType.Rust, updated.Environment.GameType);
        Assert.Equal(originalPath, updated.Environment.EnvironmentPath);

        var archived = await service.ArchiveEnvironmentAsync(
            new ArchiveEnvironmentRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                EnvironmentId = originalId.Value
            });

        Assert.True(archived.Succeeded);
        Assert.Equal(EnvironmentStatus.Archived, archived.Environment?.Status);
        Assert.Equal(originalPath, archived.Environment?.EnvironmentPath);

        var restored = await service.RestoreEnvironmentAsync(
            new RestoreEnvironmentRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                EnvironmentId = originalId.Value
            });

        Assert.True(restored.Succeeded);
        Assert.Equal(EnvironmentStatus.Draft, restored.Environment?.Status);
        Assert.Equal(originalPath, restored.Environment?.EnvironmentPath);

        var loaded = await service.LoadByWorkspaceAsync(temporaryDirectory.Path);
        var environment = Assert.Single(loaded);
        Assert.Equal(originalId, environment.Id);
        Assert.Equal("Renamed Server", environment.Name);
        Assert.Equal(originalPath, environment.EnvironmentPath);
        Assert.Equal(EnvironmentStatus.Draft, environment.Status);
    }

    [Fact]
    public async Task CreateRejectsDuplicateNamesThatMapToTheSameSlug()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();

        var first = await CreateAsync(service, temporaryDirectory.Path, "Alpha Beta");
        var duplicate = await CreateAsync(service, temporaryDirectory.Path, "Alpha_Beta");

        Assert.True(first.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Null(duplicate.Environment);
        Assert.Equal(
            "An environment with this name already exists in the current workspace.",
            duplicate.ErrorMessage);

        var loaded = await service.LoadByWorkspaceAsync(temporaryDirectory.Path);
        var persisted = Assert.Single(loaded);
        Assert.Equal(first.Environment!.Id, persisted.Id);
        Assert.Equal("Alpha Beta", persisted.Name);
    }

    [Fact]
    public async Task UpdateRejectsDuplicateDisplayName()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();
        var first = await CreateAsync(service, temporaryDirectory.Path, "First");
        var second = await CreateAsync(service, temporaryDirectory.Path, "Second");

        var result = await service.UpdateEnvironmentAsync(
            new UpdateEnvironmentRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                EnvironmentId = second.Environment!.Id.Value,
                Name = first.Environment!.Name,
                GameType = GameType.DayZ
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Environment);
        Assert.Equal(
            "An environment with this name already exists in the current workspace.",
            result.ErrorMessage);

        var loaded = await service.LoadByWorkspaceAsync(temporaryDirectory.Path);
        Assert.Collection(
            loaded,
            environment =>
            {
                Assert.Equal(first.Environment!.Id, environment.Id);
                Assert.Equal("First", environment.Name);
            },
            environment =>
            {
                Assert.Equal(second.Environment!.Id, environment.Id);
                Assert.Equal("Second", environment.Name);
            });
    }

    [Fact]
    public async Task InvalidLifecycleTransitionsReturnFailures()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();
        var created = await CreateAsync(service, temporaryDirectory.Path, "Lifecycle");
        var id = created.Environment!.Id.Value;

        var restoreDraft = await service.RestoreEnvironmentAsync(
            new RestoreEnvironmentRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                EnvironmentId = id
            });

        var firstArchive = await service.ArchiveEnvironmentAsync(
            new ArchiveEnvironmentRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                EnvironmentId = id
            });

        var secondArchive = await service.ArchiveEnvironmentAsync(
            new ArchiveEnvironmentRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                EnvironmentId = id
            });

        Assert.False(restoreDraft.Succeeded);
        Assert.Null(restoreDraft.Environment);
        Assert.Equal(
            "Only archived environments can be restored.",
            restoreDraft.ErrorMessage);
        Assert.True(firstArchive.Succeeded);
        Assert.NotNull(firstArchive.Environment);
        Assert.Equal(EnvironmentStatus.Archived, firstArchive.Environment.Status);
        Assert.False(secondArchive.Succeeded);
        Assert.Null(secondArchive.Environment);
        Assert.Equal(
            "Environment is already archived.",
            secondArchive.ErrorMessage);

        var loaded = await service.LoadByWorkspaceAsync(temporaryDirectory.Path);
        var persisted = Assert.Single(loaded);
        Assert.Equal(EnvironmentStatus.Archived, persisted.Status);
        Assert.Equal(id, persisted.Id.Value);
    }

    [Fact]
    public async Task CanceledCreateIsCurrentlyReturnedAsFailure()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService();

        var result = await service.CreateEnvironmentAsync(
            new CreateEnvironmentRequest
            {
                WorkspacePath = temporaryDirectory.Path,
                Name = "Canceled",
                GameType = GameType.DayZ
            },
            new CancellationToken(canceled: true));

        Assert.False(result.Succeeded);
        Assert.Null(result.Environment);
        Assert.Equal(
            "Failed to create environment. See logs for details.",
            result.ErrorMessage);
        Assert.Empty(await service.LoadByWorkspaceAsync(temporaryDirectory.Path));
    }

    [Fact]
    public async Task CreateRejectsInspectorInvalidWorkspacePath()
    {
        var pathInspector = new RecordingPathInspector
        {
            IsValidFullyQualifiedFolderPathResult = false
        };
        var store = new RecordingEnvironmentStore();
        var service = CreateService(store, pathInspector);
        const string workspacePath = "relative-folder";

        var result = await service.CreateEnvironmentAsync(
            new CreateEnvironmentRequest
            {
                WorkspacePath = workspacePath,
                Name = "Primary",
                GameType = GameType.DayZ
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Environment);
        Assert.Equal(
            "Workspace path must be a valid full path.",
            result.ErrorMessage);
        Assert.Equal([workspacePath], pathInspector.FullyQualifiedFolderPaths);
        Assert.Empty(pathInspector.DirectoryPaths);
        Assert.Equal(0, store.EnvironmentPathExistsCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task CreateDoesNotInspectPathWhenWorkspacePathIsMissing()
    {
        var pathInspector = new RecordingPathInspector
        {
            IsValidFullyQualifiedFolderPathResult = true
        };
        var store = new RecordingEnvironmentStore();
        var service = CreateService(store, pathInspector);

        var result = await service.CreateEnvironmentAsync(
            new CreateEnvironmentRequest
            {
                WorkspacePath = " ",
                Name = "Primary",
                GameType = GameType.DayZ
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Environment);
        Assert.Equal("Workspace path is required.", result.ErrorMessage);
        Assert.Empty(pathInspector.FullyQualifiedFolderPaths);
        Assert.Empty(pathInspector.DirectoryPaths);
        Assert.Equal(0, store.EnvironmentPathExistsCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task CreateTreatsInspectorExceptionAsInvalidPath()
    {
        var store = new RecordingEnvironmentStore();
        var service = CreateService(store, new ThrowingPathInspector());

        var result = await service.CreateEnvironmentAsync(
            new CreateEnvironmentRequest
            {
                WorkspacePath = "inspector-failure",
                Name = "Primary",
                GameType = GameType.DayZ
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Environment);
        Assert.Equal(
            "Workspace path must be a valid full path.",
            result.ErrorMessage);
        Assert.Equal(0, store.EnvironmentPathExistsCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    private static EnvironmentService CreateService()
    {
        return CreateService(new OperatingSystemPathInspector());
    }

    private static EnvironmentService CreateService(IPathInspector pathInspector)
    {
        return CreateService(
            new JsonEnvironmentStore(),
            pathInspector);
    }

    private static EnvironmentService CreateService(
        IEnvironmentStore store,
        IPathInspector pathInspector)
    {
        return new EnvironmentService(
            store,
            pathInspector,
            NullLogger<EnvironmentService>.Instance);
    }

    private static Task<CreateEnvironmentResult> CreateAsync(
        EnvironmentService service,
        string workspacePath,
        string name)
    {
        return service.CreateEnvironmentAsync(
            new CreateEnvironmentRequest
            {
                WorkspacePath = workspacePath,
                Name = name,
                GameType = GameType.DayZ
            });
    }

    private sealed class RecordingEnvironmentStore : IEnvironmentStore
    {
        public int EnvironmentPathExistsCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public Task SaveAsync(
            Deadbelt.Domain.Environments.Environment environment,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            Deadbelt.Domain.Environments.Environment environment,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Deadbelt.Domain.Environments.Environment>> LoadByWorkspaceAsync(
            string workspacePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Deadbelt.Domain.Environments.Environment>>([]);
        }

        public Task<bool> EnvironmentPathExistsAsync(
            string environmentPath,
            CancellationToken cancellationToken = default)
        {
            EnvironmentPathExistsCallCount++;
            return Task.FromResult(false);
        }
    }
}
