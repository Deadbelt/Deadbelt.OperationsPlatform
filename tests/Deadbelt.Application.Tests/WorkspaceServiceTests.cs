using Deadbelt.Application.Common;
using Deadbelt.Application.Workspaces;
using Deadbelt.Application.Tests.TestSupport;
using Deadbelt.Domain.Workspaces;
using Deadbelt.Infrastructure.FileSystem;
using Deadbelt.Infrastructure.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Application.Tests;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public async Task CreateAndOpenWorkspacePreservesCurrentMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var workspacePath = temporaryDirectory.GetPath("workspace");
        var service = CreateService(new JsonWorkspaceStore());

        var createResult = await service.CreateWorkspaceAsync(
            new CreateWorkspaceRequest
            {
                Name = "  Operations  ",
                FolderPath = workspacePath,
                Description = "  Test workspace  "
            });

        Assert.True(createResult.Succeeded);
        Assert.NotNull(createResult.Workspace);
        Assert.Equal("Operations", createResult.Workspace.Name);
        Assert.Equal("Test workspace", createResult.Workspace.Description);
        Assert.Equal("0.1", createResult.Workspace.Version);

        var openResult = await service.OpenWorkspaceAsync(
            new OpenWorkspaceRequest { FolderPath = workspacePath });

        Assert.True(openResult.Succeeded);
        Assert.NotNull(openResult.Workspace);
        Assert.Equal(createResult.Workspace.Name, openResult.Workspace.Name);
        Assert.Equal(createResult.Workspace.Path, openResult.Workspace.Path);
        Assert.Equal(createResult.Workspace.Description, openResult.Workspace.Description);
        Assert.Equal(createResult.Workspace.CreatedUtc, openResult.Workspace.CreatedUtc);
        Assert.Equal(createResult.Workspace.Version, openResult.Workspace.Version);
    }

    [Theory]
    [InlineData("", "Workspace folder is required.")]
    [InlineData("relative-folder", "Workspace folder must be a valid full path.")]
    public async Task CreateRejectsInvalidWorkspacePath(
        string folderPath,
        string expectedErrorMessage)
    {
        var store = new RecordingWorkspaceStore();
        var service = CreateService(store);

        var result = await service.CreateWorkspaceAsync(
            new CreateWorkspaceRequest
            {
                Name = "Operations",
                FolderPath = folderPath
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Workspace);
        Assert.Equal(expectedErrorMessage, result.ErrorMessage);
        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(0, store.LoadCallCount);
    }

    [Theory]
    [InlineData("", "Workspace folder is required.")]
    [InlineData("relative-folder", "Workspace folder must be a valid full path.")]
    public async Task OpenRejectsInvalidWorkspacePath(
        string folderPath,
        string expectedErrorMessage)
    {
        var store = new RecordingWorkspaceStore();
        var service = CreateService(store);

        var result = await service.OpenWorkspaceAsync(
            new OpenWorkspaceRequest { FolderPath = folderPath });

        Assert.False(result.Succeeded);
        Assert.Null(result.Workspace);
        Assert.Equal(expectedErrorMessage, result.ErrorMessage);
        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(0, store.LoadCallCount);
    }

    [Fact]
    public async Task OpenFailsWhenWorkspaceMetadataIsMissing()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService(new JsonWorkspaceStore());

        var result = await service.OpenWorkspaceAsync(
            new OpenWorkspaceRequest { FolderPath = temporaryDirectory.Path });

        Assert.False(result.Succeeded);
        Assert.Null(result.Workspace);
        Assert.Equal(
            "The selected folder is not a valid Deadbelt workspace.",
            result.ErrorMessage);
    }

    [Fact]
    public async Task CancellationThrownByStoreIsCurrentlyReturnedAsFailure()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var service = CreateService(new CancellingWorkspaceStore());

        var result = await service.CreateWorkspaceAsync(
            new CreateWorkspaceRequest
            {
                Name = "Operations",
                FolderPath = temporaryDirectory.GetPath("workspace")
            },
            new CancellationToken(canceled: true));

        Assert.False(result.Succeeded);
        Assert.Null(result.Workspace);
        Assert.Equal(
            "Failed to create workspace. See logs for details.",
            result.ErrorMessage);
    }

    [Fact]
    public async Task CreateUsesPathInspectorAndSavesWhenPathIsValid()
    {
        var pathInspector = new RecordingPathInspector
        {
            IsValidFullyQualifiedFolderPathResult = true
        };
        var store = new RecordingWorkspaceStore();
        var service = CreateService(store, pathInspector);
        const string folderPath = "inspector-approved-path";

        var result = await service.CreateWorkspaceAsync(
            new CreateWorkspaceRequest
            {
                Name = "Operations",
                FolderPath = folderPath
            });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Workspace);
        Assert.Equal(folderPath, result.Workspace.Path);
        Assert.Equal([folderPath], pathInspector.FullyQualifiedFolderPaths);
        Assert.Empty(pathInspector.DirectoryPaths);
        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(0, store.LoadCallCount);
    }

    [Theory]
    [InlineData("relative-folder")]
    [InlineData("invalid\0path")]
    public async Task CreateRejectsInspectorInvalidPathsWithoutCallingStore(string folderPath)
    {
        var pathInspector = new RecordingPathInspector
        {
            IsValidFullyQualifiedFolderPathResult = false
        };
        var store = new RecordingWorkspaceStore();
        var service = CreateService(store, pathInspector);

        var result = await service.CreateWorkspaceAsync(
            new CreateWorkspaceRequest
            {
                Name = "Operations",
                FolderPath = folderPath
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Workspace);
        Assert.Equal(
            "Workspace folder must be a valid full path.",
            result.ErrorMessage);
        Assert.Equal([folderPath], pathInspector.FullyQualifiedFolderPaths);
        Assert.Empty(pathInspector.DirectoryPaths);
        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(0, store.LoadCallCount);
    }

    [Fact]
    public async Task CreateDoesNotInspectPathWhenNameIsMissing()
    {
        var pathInspector = new RecordingPathInspector
        {
            IsValidFullyQualifiedFolderPathResult = true
        };
        var store = new RecordingWorkspaceStore();
        var service = CreateService(store, pathInspector);

        var result = await service.CreateWorkspaceAsync(
            new CreateWorkspaceRequest
            {
                Name = " ",
                FolderPath = "inspector-approved-path"
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Workspace);
        Assert.Equal("Workspace name is required.", result.ErrorMessage);
        Assert.Empty(pathInspector.FullyQualifiedFolderPaths);
        Assert.Empty(pathInspector.DirectoryPaths);
        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(0, store.LoadCallCount);
    }

    [Fact]
    public async Task CreateTreatsInspectorExceptionAsInvalidPath()
    {
        var store = new RecordingWorkspaceStore();
        var service = CreateService(store, new ThrowingPathInspector());

        var result = await service.CreateWorkspaceAsync(
            new CreateWorkspaceRequest
            {
                Name = "Operations",
                FolderPath = "inspector-failure"
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Workspace);
        Assert.Equal(
            "Workspace folder must be a valid full path.",
            result.ErrorMessage);
        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(0, store.LoadCallCount);
    }

    private static WorkspaceService CreateService(IWorkspaceStore store)
    {
        return CreateService(
            store,
            new OperatingSystemPathInspector());
    }

    private static WorkspaceService CreateService(
        IWorkspaceStore store,
        IPathInspector pathInspector)
    {
        return new WorkspaceService(
            store,
            pathInspector,
            NullLogger<WorkspaceService>.Instance);
    }

    private sealed class CancellingWorkspaceStore : IWorkspaceStore
    {
        public Task SaveAsync(
            Workspace workspace,
            CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        public Task<Workspace?> LoadAsync(
            string folderPath,
            CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class RecordingWorkspaceStore : IWorkspaceStore
    {
        public int SaveCallCount { get; private set; }

        public int LoadCallCount { get; private set; }

        public Task SaveAsync(
            Workspace workspace,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.CompletedTask;
        }

        public Task<Workspace?> LoadAsync(
            string folderPath,
            CancellationToken cancellationToken = default)
        {
            LoadCallCount++;
            return Task.FromResult<Workspace?>(null);
        }
    }
}
