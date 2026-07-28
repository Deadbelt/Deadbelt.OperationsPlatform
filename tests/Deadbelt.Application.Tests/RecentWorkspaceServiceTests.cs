using Deadbelt.Application.Workspaces;
using Deadbelt.Domain.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Application.Tests;

public sealed class RecentWorkspaceServiceTests
{
    [Fact]
    public async Task GetOrdersNewestFirstAndLimitsHistoryToTenEntries()
    {
        var workspaces = Enumerable
            .Range(0, 12)
            .Select(index => new RecentWorkspace(
                $"Workspace {index}",
                $"C:\\workspaces\\{index}",
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddDays(index)))
            .ToArray();
        var store = new RecordingRecentWorkspaceStore(workspaces);
        var service = CreateService(store);

        var result = await service.GetRecentWorkspacesAsync();

        Assert.Equal(10, result.Count);
        Assert.Equal("Workspace 11", result[0].Name);
        Assert.Equal("Workspace 2", result[^1].Name);
        Assert.True(result.Zip(
            result.Skip(1),
            (newer, older) => newer.LastOpenedUtc >= older.LastOpenedUtc)
            .All(isDescending => isDescending));
    }

    [Fact]
    public async Task RecordDeduplicatesPathsCaseInsensitivelyAndPlacesRecordedWorkspaceFirst()
    {
        var existing = new[]
        {
            new RecentWorkspace(
                "Old Alpha",
                "c:\\workspaces\\alpha",
                DateTime.MinValue),
            new RecentWorkspace(
                "Beta",
                "C:\\workspaces\\beta",
                DateTime.MinValue.AddTicks(1))
        };
        var store = new RecordingRecentWorkspaceStore(existing);
        var service = CreateService(store);
        var workspace = new Workspace(
            "Current Alpha",
            "C:\\Workspaces\\Alpha",
            null,
            DateTime.MinValue,
            "0.1");

        await service.RecordWorkspaceAsync(workspace);

        Assert.NotNull(store.LastSaved);
        Assert.Collection(
            store.LastSaved,
            recorded =>
            {
                Assert.Equal("Current Alpha", recorded.Name);
                Assert.Equal("C:\\Workspaces\\Alpha", recorded.Path);
            },
            retained =>
            {
                Assert.Equal("Beta", retained.Name);
                Assert.Equal("C:\\workspaces\\beta", retained.Path);
            });
    }

    [Fact]
    public async Task RecordLimitsSavedHistoryToTenEntries()
    {
        var existing = Enumerable
            .Range(0, 10)
            .Select(index => new RecentWorkspace(
                $"Workspace {index}",
                $"C:\\workspaces\\{index}",
                DateTime.MinValue.AddTicks(index)))
            .ToArray();
        var store = new RecordingRecentWorkspaceStore(existing);
        var service = CreateService(store);
        var workspace = new Workspace(
            "Newest",
            "C:\\workspaces\\newest",
            null,
            DateTime.MinValue,
            "0.1");

        await service.RecordWorkspaceAsync(workspace);

        Assert.NotNull(store.LastSaved);
        Assert.Equal(10, store.LastSaved.Count);
        Assert.Equal("Newest", store.LastSaved[0].Name);
        Assert.DoesNotContain(
            store.LastSaved,
            recent => recent.Name == "Workspace 0");
    }

    [Fact]
    public async Task RemoveDeletesMatchingPathCaseInsensitivelyAndSortsRemainingHistory()
    {
        var store = new RecordingRecentWorkspaceStore(
            [
                new RecentWorkspace(
                    "Oldest",
                    "C:\\workspaces\\oldest",
                    DateTime.MinValue),
                new RecentWorkspace(
                    "Remove",
                    "C:\\workspaces\\remove",
                    DateTime.MinValue.AddTicks(1)),
                new RecentWorkspace(
                    "Newest",
                    "C:\\workspaces\\newest",
                    DateTime.MinValue.AddTicks(2))
            ]);
        var service = CreateService(store);

        await service.RemoveWorkspaceAsync("c:\\WORKSPACES\\REMOVE");

        Assert.NotNull(store.LastSaved);
        Assert.Collection(
            store.LastSaved,
            recent => Assert.Equal("Newest", recent.Name),
            recent => Assert.Equal("Oldest", recent.Name));
    }

    [Fact]
    public async Task GetSuppressesStoreLoadErrorsAndReturnsEmptyHistory()
    {
        var service = CreateService(
            new ThrowingRecentWorkspaceStore(throwOnLoad: true));

        var result = await service.GetRecentWorkspacesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task RecordAndRemoveSuppressStoreSaveErrors()
    {
        var store = new ThrowingRecentWorkspaceStore(throwOnSave: true);
        var service = CreateService(store);
        var workspace = new Workspace(
            "Operations",
            "C:\\workspaces\\operations",
            null,
            DateTime.MinValue,
            "0.1");

        var recordException = await Record.ExceptionAsync(
            () => service.RecordWorkspaceAsync(workspace));
        var removeException = await Record.ExceptionAsync(
            () => service.RemoveWorkspaceAsync(workspace.Path));

        Assert.Null(recordException);
        Assert.Null(removeException);
    }

    private static RecentWorkspaceService CreateService(
        IRecentWorkspaceStore store)
    {
        return new RecentWorkspaceService(
            store,
            NullLogger<RecentWorkspaceService>.Instance);
    }

    private sealed class RecordingRecentWorkspaceStore : IRecentWorkspaceStore
    {
        private readonly IReadOnlyList<RecentWorkspace> _workspaces;

        public RecordingRecentWorkspaceStore(
            IReadOnlyList<RecentWorkspace> workspaces)
        {
            _workspaces = workspaces;
        }

        public IReadOnlyList<RecentWorkspace>? LastSaved { get; private set; }

        public Task<IReadOnlyList<RecentWorkspace>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_workspaces);
        }

        public Task SaveAsync(
            IReadOnlyList<RecentWorkspace> recentWorkspaces,
            CancellationToken cancellationToken = default)
        {
            LastSaved = recentWorkspaces.ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingRecentWorkspaceStore : IRecentWorkspaceStore
    {
        private readonly bool _throwOnLoad;
        private readonly bool _throwOnSave;

        public ThrowingRecentWorkspaceStore(
            bool throwOnLoad = false,
            bool throwOnSave = false)
        {
            _throwOnLoad = throwOnLoad;
            _throwOnSave = throwOnSave;
        }

        public Task<IReadOnlyList<RecentWorkspace>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            if (_throwOnLoad)
                throw new IOException("Characterized load failure.");

            return Task.FromResult<IReadOnlyList<RecentWorkspace>>(
                Array.Empty<RecentWorkspace>());
        }

        public Task SaveAsync(
            IReadOnlyList<RecentWorkspace> recentWorkspaces,
            CancellationToken cancellationToken = default)
        {
            if (_throwOnSave)
                throw new IOException("Characterized save failure.");

            return Task.CompletedTask;
        }
    }
}
