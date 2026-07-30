using System.IO;
using System.Windows;
using Deadbelt.Application.Doctor;
using Deadbelt.Application.Environments;
using Deadbelt.Application.Persistence;
using Deadbelt.Application.Providers;
using Deadbelt.Application.Workspaces;
using Deadbelt.Desktop.Services;
using Deadbelt.Desktop.ViewModels;
using Deadbelt.Domain.Doctor;
using Deadbelt.Domain.Providers;
using Deadbelt.Domain.Workspaces;
using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Desktop.Tests;

public sealed class MainWindowViewModelPersistenceDiagnosticTests
{
    [Fact]
    public void SuccessfulWorkspaceCreationTransitionClearsStaleDiagnostics()
    {
        var viewModel = CreateViewModel();
        viewModel.SetActiveWorkspace(CreateWorkspace("Old"));
        viewModel.ReplacePersistenceDiagnostics(
            PersistenceResourceCategory.Environment,
            [CreateWarning(PersistenceResourceCategory.Environment, "old")]);

        viewModel.SetActiveWorkspace(CreateWorkspace("Created"));

        Assert.True(viewModel.IsWorkspaceOpen);
        Assert.Equal("Created", viewModel.ActiveWorkspaceName);
        Assert.Empty(viewModel.PersistenceDiagnostics);
        Assert.False(viewModel.HasPersistenceDiagnostics);
        Assert.Equal(0, viewModel.PersistenceDiagnosticCount);
    }

    [Fact]
    public async Task SuccessfulOpenClearsStaleDiagnosticsAndAppliesResourceWarnings()
    {
        var environmentWarning = CreateWarning(
            PersistenceResourceCategory.Environment,
            "new-environment");
        var providerWarning = CreateWarning(
            PersistenceResourceCategory.Provider,
            "new-provider");
        var environmentService = new StubEnvironmentService(
            PersistenceLoadResult<IReadOnlyList<DOPEnvironment>>.Success(
                Array.Empty<DOPEnvironment>(),
                [environmentWarning]));
        var providerService = new StubProviderService(
            PersistenceLoadResult<IReadOnlyList<Provider>>.Success(
                Array.Empty<Provider>(),
                [providerWarning]));
        var viewModel = CreateViewModel(
            environmentService,
            providerService);
        viewModel.SetActiveWorkspace(CreateWorkspace("Old"));
        viewModel.ReplacePersistenceDiagnostics(
            PersistenceResourceCategory.RecentWorkspaces,
            [CreateWarning(PersistenceResourceCategory.RecentWorkspaces, "old")]);

        var activated = await viewModel.TryActivateOpenedWorkspaceAsync(
            OpenWorkspaceResult.Success(CreateWorkspace("Opened")));

        Assert.True(activated);
        Assert.Equal("Opened", viewModel.ActiveWorkspaceName);
        Assert.Equal(2, viewModel.PersistenceDiagnosticCount);
        Assert.Collection(
            viewModel.PersistenceDiagnostics.OrderBy(item => item.Resource),
            item => Assert.Equal("Environment", item.Resource),
            item => Assert.Equal("Provider", item.Resource));
    }

    [Fact]
    public async Task FailedOpenPreservesActiveWorkspaceAndDiagnostics()
    {
        var viewModel = CreateViewModel();
        viewModel.SetActiveWorkspace(CreateWorkspace("Current"));
        var warning = CreateWarning(
            PersistenceResourceCategory.Environment,
            "current");
        viewModel.ReplacePersistenceDiagnostics(
            PersistenceResourceCategory.Environment,
            [warning]);
        var originalSummary = Assert.Single(viewModel.PersistenceDiagnostics);

        var activated = await viewModel.TryActivateOpenedWorkspaceAsync(
            OpenWorkspaceResult.Failure("Open failed."));

        Assert.False(activated);
        Assert.Equal("Current", viewModel.ActiveWorkspaceName);
        Assert.Same(
            originalSummary,
            Assert.Single(viewModel.PersistenceDiagnostics));
        Assert.Equal(1, viewModel.PersistenceDiagnosticCount);
    }

    [Fact]
    public void CategoryReplacementPreservesUnrelatedCategories()
    {
        var viewModel = CreateViewModel();
        var firstEnvironmentWarning = CreateWarning(
            PersistenceResourceCategory.Environment,
            "environment-one");
        var secondEnvironmentWarning = CreateWarning(
            PersistenceResourceCategory.Environment,
            "environment-two");
        var providerWarning = CreateWarning(
            PersistenceResourceCategory.Provider,
            "provider");
        viewModel.ReplacePersistenceDiagnostics(
            PersistenceResourceCategory.Environment,
            [firstEnvironmentWarning]);
        viewModel.ReplacePersistenceDiagnostics(
            PersistenceResourceCategory.Provider,
            [providerWarning]);

        viewModel.ReplacePersistenceDiagnostics(
            PersistenceResourceCategory.Environment,
            [secondEnvironmentWarning]);

        Assert.Equal(2, viewModel.PersistenceDiagnosticCount);
        Assert.DoesNotContain(
            viewModel.PersistenceDiagnostics,
            item => item.SourcePath.EndsWith(
                "environment-one.json",
                StringComparison.Ordinal));
        Assert.Contains(
            viewModel.PersistenceDiagnostics,
            item => item.SourcePath.EndsWith(
                "environment-two.json",
                StringComparison.Ordinal));
        Assert.Contains(
            viewModel.PersistenceDiagnostics,
            item => item.Resource == "Provider");
    }

    [Fact]
    public void DuplicateWarningsAreCollapsedAndCountMatchesVisibleCollection()
    {
        var viewModel = CreateViewModel();
        var warning = CreateWarning(
            PersistenceResourceCategory.Provider,
            "duplicate");

        viewModel.ReplacePersistenceDiagnostics(
            PersistenceResourceCategory.Provider,
            [warning, warning]);

        Assert.Single(viewModel.PersistenceDiagnostics);
        Assert.True(viewModel.HasPersistenceDiagnostics);
        Assert.Equal(
            viewModel.PersistenceDiagnostics.Count,
            viewModel.PersistenceDiagnosticCount);
        Assert.Equal(
            "Loading completed with 1 warning(s).",
            viewModel.PersistenceDiagnosticTitle);
    }

    [Fact]
    public void UnloadClearsWorkspaceDiagnosticsAndEmptyState()
    {
        var viewModel = CreateViewModel();
        viewModel.SetActiveWorkspace(CreateWorkspace("Current"));
        viewModel.ReplacePersistenceDiagnostics(
            PersistenceResourceCategory.Provider,
            [CreateWarning(PersistenceResourceCategory.Provider, "current")]);

        viewModel.UnloadActiveWorkspace();

        Assert.False(viewModel.IsWorkspaceOpen);
        Assert.Equal("None", viewModel.ActiveWorkspaceName);
        Assert.Empty(viewModel.PersistenceDiagnostics);
        Assert.False(viewModel.HasPersistenceDiagnostics);
        Assert.Equal(0, viewModel.PersistenceDiagnosticCount);
    }

    private static MainWindowViewModel CreateViewModel(
        IEnvironmentService? environmentService = null,
        IProviderService? providerService = null)
    {
        var dialogs = new StubDialogServices();

        return new MainWindowViewModel(
            new StubWorkspaceService(),
            dialogs,
            new StubRecentWorkspaceService(),
            providerService ?? new StubProviderService(),
            dialogs,
            dialogs,
            environmentService ?? new StubEnvironmentService(),
            dialogs,
            dialogs,
            new DoctorViewModel(
                new StubDoctorService(),
                new StubDoctorPathDialogService()));
    }

    private static Workspace CreateWorkspace(string name)
    {
        return new Workspace(
            name,
            Path.Combine("C:\\workspaces", name),
            null,
            new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            "0.1");
    }

    private static PersistenceDiagnostic CreateWarning(
        PersistenceResourceCategory resourceCategory,
        string name)
    {
        return new PersistenceDiagnostic(
            $"DOP.Persistence.Test.{name}",
            PersistenceDiagnosticSeverity.Warning,
            resourceCategory,
            Path.Combine("C:\\metadata", $"{name}.json"),
            $"Safe warning for {name}.");
    }

    private sealed class StubWorkspaceService : IWorkspaceService
    {
        public Task<CreateWorkspaceResult> CreateWorkspaceAsync(
            CreateWorkspaceRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OpenWorkspaceResult> OpenWorkspaceAsync(
            OpenWorkspaceRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubRecentWorkspaceService : IRecentWorkspaceService
    {
        public Task<PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>> GetRecentWorkspacesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>.Success(
                    Array.Empty<RecentWorkspace>()));
        }

        public Task RecordWorkspaceAsync(
            Workspace workspace,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveWorkspaceAsync(
            string workspacePath,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubEnvironmentService : IEnvironmentService
    {
        private readonly PersistenceLoadResult<IReadOnlyList<DOPEnvironment>> _loadResult;

        public StubEnvironmentService(
            PersistenceLoadResult<IReadOnlyList<DOPEnvironment>>? loadResult = null)
        {
            _loadResult = loadResult
                ?? PersistenceLoadResult<IReadOnlyList<DOPEnvironment>>.Success(
                    Array.Empty<DOPEnvironment>());
        }

        public Task<CreateEnvironmentResult> CreateEnvironmentAsync(
            CreateEnvironmentRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<UpdateEnvironmentResult> UpdateEnvironmentAsync(
            UpdateEnvironmentRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ArchiveEnvironmentResult> ArchiveEnvironmentAsync(
            ArchiveEnvironmentRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RestoreEnvironmentResult> RestoreEnvironmentAsync(
            RestoreEnvironmentRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PersistenceLoadResult<IReadOnlyList<DOPEnvironment>>> LoadByWorkspaceAsync(
            string workspacePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_loadResult);
        }
    }

    private sealed class StubProviderService : IProviderService
    {
        private readonly PersistenceLoadResult<IReadOnlyList<Provider>> _loadResult;

        public StubProviderService(
            PersistenceLoadResult<IReadOnlyList<Provider>>? loadResult = null)
        {
            _loadResult = loadResult
                ?? PersistenceLoadResult<IReadOnlyList<Provider>>.Success(
                    Array.Empty<Provider>());
        }

        public Task<CreateProviderResult> CreateProviderAsync(
            CreateProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<UpdateProviderResult> UpdateProviderAsync(
            UpdateProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ArchiveProviderResult> ArchiveProviderAsync(
            ArchiveProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RestoreProviderResult> RestoreProviderAsync(
            RestoreProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PersistenceLoadResult<IReadOnlyList<Provider>>> LoadByWorkspaceAsync(
            string workspacePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_loadResult);
        }
    }

    private sealed class StubDoctorService : IDoctorService
    {
        public Task<DoctorScanResult> ScanAsync(
            DoctorScanRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubDoctorPathDialogService : IDoctorPathDialogService
    {
        public string? SelectFolder(
            string title,
            string? initialPath = null)
        {
            return null;
        }

        public string? SelectFile(
            string title,
            string filter,
            string? initialPath = null)
        {
            return null;
        }
    }

    private sealed class StubDialogServices :
        IWorkspaceDialogService,
        IProviderDialogService,
        IEditProviderDialogService,
        IEnvironmentDialogService,
        IEditEnvironmentDialogService
    {
        public WorkspaceDialogResult ShowCreateWorkspaceDialog(Window owner)
        {
            throw new NotSupportedException();
        }

        public string? ShowOpenWorkspaceDialog(Window owner)
        {
            throw new NotSupportedException();
        }

        public CreateProviderDialogResult ShowCreateProviderDialog(Window owner)
        {
            throw new NotSupportedException();
        }

        public EditProviderDialogResult ShowEditProviderDialog(
            Window owner,
            ProviderSummaryViewModel provider)
        {
            throw new NotSupportedException();
        }

        public EnvironmentDialogResult ShowCreateEnvironmentDialog(Window owner)
        {
            throw new NotSupportedException();
        }

        public EditEnvironmentDialogResult ShowEditEnvironmentDialog(
            Window owner,
            EnvironmentSummaryViewModel environment)
        {
            throw new NotSupportedException();
        }
    }
}
