using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Deadbelt.Application.Environments;
using Deadbelt.Application.Workspaces;
using Deadbelt.Desktop.MVVM;
using Deadbelt.Desktop.Services;
using Deadbelt.Domain.Workspaces;

namespace Deadbelt.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private const string OverviewSection = "Overview";
    private const string EnvironmentsSection = "Environments";
    private const string ProvidersSection = "Providers";
    private const string JobsSection = "Jobs";
    private const string SettingsSection = "Settings";

    private readonly IWorkspaceService _workspaceService;
    private readonly IWorkspaceDialogService _workspaceDialogService;
    private readonly IRecentWorkspaceService _recentWorkspaceService;
    private readonly IEnvironmentService _environmentService;
    private readonly IEnvironmentDialogService _environmentDialogService;
    private readonly IEditEnvironmentDialogService _editEnvironmentDialogService;

    private readonly List<EnvironmentSummaryViewModel> _allEnvironments = [];

    private Workspace? _activeWorkspace;
    private EnvironmentSummaryViewModel? _selectedEnvironment;
    private RecentWorkspaceSummaryViewModel? _selectedRecentWorkspace;
    private EnvironmentStatusFilterViewModel? _selectedEnvironmentStatusFilter;

    private string _selectedNavigationSection = OverviewSection;
    private string _workspaceStatus = "Workspace: None";
    private string _welcomeMessage = "No workspace is currently open.";
    private string _statusMessage = "Ready";

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        IWorkspaceDialogService workspaceDialogService,
        IRecentWorkspaceService recentWorkspaceService,
        IEnvironmentService environmentService,
        IEnvironmentDialogService environmentDialogService,
        IEditEnvironmentDialogService editEnvironmentDialogService)
    {
        _workspaceService = workspaceService;
        _workspaceDialogService = workspaceDialogService;
        _recentWorkspaceService = recentWorkspaceService;
        _environmentService = environmentService;
        _environmentDialogService = environmentDialogService;
        _editEnvironmentDialogService = editEnvironmentDialogService;

        CreateWorkspaceCommand = new AsyncRelayCommand(CreateWorkspaceAsync);
        OpenWorkspaceCommand = new AsyncRelayCommand(OpenWorkspaceAsync);

        OpenRecentWorkspaceCommand = new AsyncRelayCommand(
            OpenRecentWorkspaceAsync,
            CanOpenSelectedRecentWorkspace);

        RemoveRecentWorkspaceCommand = new AsyncRelayCommand(
            RemoveRecentWorkspaceAsync,
            CanRemoveSelectedRecentWorkspace);

        CreateEnvironmentCommand = new AsyncRelayCommand(
            CreateEnvironmentAsync,
            () => IsWorkspaceOpen);

        EditEnvironmentCommand = new AsyncRelayCommand(
            EditEnvironmentAsync,
            () => IsWorkspaceOpen && SelectedEnvironment is not null);

        ArchiveEnvironmentCommand = new AsyncRelayCommand(
            ArchiveEnvironmentAsync,
            CanArchiveSelectedEnvironment);

        RestoreEnvironmentCommand = new AsyncRelayCommand(
            RestoreEnvironmentAsync,
            CanRestoreSelectedEnvironment);

        NavigateOverviewCommand = new RelayCommand(() => NavigateTo(OverviewSection));
        NavigateEnvironmentsCommand = new RelayCommand(() => NavigateTo(EnvironmentsSection));
        NavigateProvidersCommand = new RelayCommand(() => NavigateTo(ProvidersSection));
        NavigateJobsCommand = new RelayCommand(() => NavigateTo(JobsSection));
        NavigateSettingsCommand = new RelayCommand(() => NavigateTo(SettingsSection));

        foreach (var filter in EnvironmentStatusFilterViewModel.CreateDefaultFilters())
        {
            EnvironmentStatusFilters.Add(filter);
        }

        _selectedEnvironmentStatusFilter = EnvironmentStatusFilters.FirstOrDefault();

        _ = LoadRecentWorkspacesAsync();
    }

    public string ApplicationName => "DEADBELT";

    public string ApplicationSubtitle => "Operations Platform";

    public string ApplicationVersion => "v0.2.0-prealpha";

    public bool IsWorkspaceOpen => _activeWorkspace is not null;

    public string ActiveWorkspaceName => _activeWorkspace?.Name ?? "None";

    public string ActiveWorkspacePath => _activeWorkspace?.Path ?? string.Empty;

    public string ActiveWorkspaceVersion => _activeWorkspace?.Version ?? string.Empty;

    public ObservableCollection<EnvironmentStatusFilterViewModel> EnvironmentStatusFilters { get; } = [];

    public ObservableCollection<EnvironmentSummaryViewModel> Environments { get; } = [];

    public ObservableCollection<RecentWorkspaceSummaryViewModel> RecentWorkspaces { get; } = [];

    public EnvironmentStatusFilterViewModel? SelectedEnvironmentStatusFilter
    {
        get => _selectedEnvironmentStatusFilter;
        set
        {
            if (SetProperty(ref _selectedEnvironmentStatusFilter, value))
            {
                ApplyEnvironmentFilter();
            }
        }
    }

    public int EnvironmentCount => _allEnvironments.Count;

    public bool HasEnvironments => _allEnvironments.Count > 0;

    public bool HasVisibleEnvironments => Environments.Count > 0;

    public bool HasRecentWorkspaces => RecentWorkspaces.Count > 0;
    public bool CanOpenSelectedRecentWorkspace()
    {
        return SelectedRecentWorkspace is not null
            && !SelectedRecentWorkspace.IsActive;
    }


    public RecentWorkspaceSummaryViewModel? SelectedRecentWorkspace
    {
        get => _selectedRecentWorkspace;
        set
        {
            if (SetProperty(ref _selectedRecentWorkspace, value))
            {
                OpenRecentWorkspaceCommand.RaiseCanExecuteChanged();
                RemoveRecentWorkspaceCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanRemoveSelectedRecentWorkspace()
    {
        return SelectedRecentWorkspace is not null;
    }

    private async Task RemoveRecentWorkspaceAsync()
    {
        if (SelectedRecentWorkspace is null)
            return;

        var workspaceToRemove = SelectedRecentWorkspace;

        var result = MessageBox.Show(
            $"Remove '{workspaceToRemove.Name}' from recent workspaces?\n\nThis only removes the workspace from recent history. It does not delete any files.",
            "Remove Recent Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        await _recentWorkspaceService.RemoveWorkspaceAsync(workspaceToRemove.Path);

        await LoadRecentWorkspacesAsync();

        StatusMessage = $"Removed recent workspace: {workspaceToRemove.Name}";
    }

    public EnvironmentSummaryViewModel? SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            if (SetProperty(ref _selectedEnvironment, value))
            {
                OnPropertyChanged(nameof(HasSelectedEnvironment));
                OnPropertyChanged(nameof(CanArchiveSelectedEnvironment));
                OnPropertyChanged(nameof(CanRestoreSelectedEnvironment));

                EditEnvironmentCommand.RaiseCanExecuteChanged();
                ArchiveEnvironmentCommand.RaiseCanExecuteChanged();
                RestoreEnvironmentCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedEnvironment => SelectedEnvironment is not null;

    public bool CanArchiveSelectedEnvironment()
    {
        return IsWorkspaceOpen
            && SelectedEnvironment is not null
            && !SelectedEnvironment.IsArchived;
    }

    public bool CanRestoreSelectedEnvironment()
    {
        return IsWorkspaceOpen
            && SelectedEnvironment is not null
            && SelectedEnvironment.IsArchived;
    }

    public string SelectedNavigationSection
    {
        get => _selectedNavigationSection;
        private set
        {
            if (SetProperty(ref _selectedNavigationSection, value))
            {
                OnPropertyChanged(nameof(IsOverviewSelected));
                OnPropertyChanged(nameof(IsEnvironmentsSelected));
                OnPropertyChanged(nameof(IsProvidersSelected));
                OnPropertyChanged(nameof(IsJobsSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
            }
        }
    }

    public bool IsOverviewSelected => SelectedNavigationSection == OverviewSection;

    public bool IsEnvironmentsSelected => SelectedNavigationSection == EnvironmentsSection;

    public bool IsProvidersSelected => SelectedNavigationSection == ProvidersSection;

    public bool IsJobsSelected => SelectedNavigationSection == JobsSection;

    public bool IsSettingsSelected => SelectedNavigationSection == SettingsSection;

    public string WorkspaceStatus
    {
        get => _workspaceStatus;
        private set => SetProperty(ref _workspaceStatus, value);
    }

    public string WelcomeMessage
    {
        get => _welcomeMessage;
        private set => SetProperty(ref _welcomeMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand CreateWorkspaceCommand { get; }

    public ICommand OpenWorkspaceCommand { get; }

    public AsyncRelayCommand OpenRecentWorkspaceCommand { get; }

    public AsyncRelayCommand RemoveRecentWorkspaceCommand { get; }

    public AsyncRelayCommand CreateEnvironmentCommand { get; }

    public AsyncRelayCommand EditEnvironmentCommand { get; }

    public AsyncRelayCommand ArchiveEnvironmentCommand { get; }

    public AsyncRelayCommand RestoreEnvironmentCommand { get; }

    public ICommand NavigateOverviewCommand { get; }

    public ICommand NavigateEnvironmentsCommand { get; }

    public ICommand NavigateProvidersCommand { get; }

    public ICommand NavigateJobsCommand { get; }

    public ICommand NavigateSettingsCommand { get; }

    private async Task CreateWorkspaceAsync()
    {
        var owner = System.Windows.Application.Current.MainWindow;

        if (owner is null)
        {
            StatusMessage = "Unable to open workspace dialog.";
            return;
        }

        var dialogResult = _workspaceDialogService.ShowCreateWorkspaceDialog(owner);

        if (!dialogResult.Confirmed)
        {
            StatusMessage = "Workspace creation cancelled.";
            return;
        }

        StatusMessage = "Creating workspace...";

        var result = await _workspaceService.CreateWorkspaceAsync(
            new CreateWorkspaceRequest
            {
                Name = dialogResult.Name,
                FolderPath = dialogResult.FolderPath,
                Description = dialogResult.Description
            });

        if (!result.Succeeded || result.Workspace is null)
        {
            StatusMessage = "Failed to create workspace.";

            MessageBox.Show(
                result.ErrorMessage ?? "Failed to create workspace.",
                "Deadbelt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        SetActiveWorkspace(result.Workspace);

        await RecordActiveWorkspaceAsRecentAsync(result.Workspace);

        StatusMessage = "Workspace created.";
    }

    private async Task OpenWorkspaceAsync()
    {
        var owner = System.Windows.Application.Current.MainWindow;

        if (owner is null)
        {
            StatusMessage = "Unable to open workspace dialog.";
            return;
        }

        var folderPath = _workspaceDialogService.ShowOpenWorkspaceDialog(owner);

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            StatusMessage = "Open workspace cancelled.";
            return;
        }

        await OpenWorkspaceFromPathAsync(folderPath);
    }

    private async Task OpenRecentWorkspaceAsync()
    {
        if (SelectedRecentWorkspace is null)
        {
            StatusMessage = "No recent workspace is selected.";
            return;
        }

        await OpenWorkspaceFromPathAsync(SelectedRecentWorkspace.Path);
    }

    private async Task OpenWorkspaceFromPathAsync(string folderPath)
    {
        StatusMessage = "Opening workspace...";

        var result = await _workspaceService.OpenWorkspaceAsync(
            new OpenWorkspaceRequest
            {
                FolderPath = folderPath
            });

        if (!result.Succeeded || result.Workspace is null)
        {
            StatusMessage = "Failed to open workspace.";

            MessageBox.Show(
                result.ErrorMessage ?? "Failed to open workspace.",
                "Deadbelt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            await LoadRecentWorkspacesAsync();

            return;
        }

        SetActiveWorkspace(result.Workspace);

        await LoadActiveWorkspaceEnvironmentsAsync();

        await RecordActiveWorkspaceAsRecentAsync(result.Workspace);

        StatusMessage = $"Workspace opened. Loaded {EnvironmentCount} environment(s).";
    }

    private async Task CreateEnvironmentAsync()
    {
        if (_activeWorkspace is null)
        {
            StatusMessage = "No workspace is currently open.";
            return;
        }

        var owner = System.Windows.Application.Current.MainWindow;

        if (owner is null)
        {
            StatusMessage = "Unable to open environment dialog.";
            return;
        }

        var dialogResult = _environmentDialogService.ShowCreateEnvironmentDialog(owner);

        if (!dialogResult.Confirmed)
        {
            StatusMessage = "Environment creation cancelled.";
            return;
        }

        StatusMessage = "Creating environment...";

        var result = await _environmentService.CreateEnvironmentAsync(
            new CreateEnvironmentRequest
            {
                WorkspacePath = _activeWorkspace.Path,
                Name = dialogResult.Name,
                Description = dialogResult.Description,
                GameType = dialogResult.GameType
            });

        if (!result.Succeeded || result.Environment is null)
        {
            StatusMessage = "Failed to create environment.";

            MessageBox.Show(
                result.ErrorMessage ?? "Failed to create environment.",
                "Deadbelt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        var environmentSummary = EnvironmentSummaryViewModel.FromEnvironment(result.Environment);

        _allEnvironments.Add(environmentSummary);

        ApplyEnvironmentFilter(environmentSummary);

        NavigateTo(EnvironmentsSection);

        StatusMessage = "Environment created.";
    }

    private async Task EditEnvironmentAsync()
    {
        if (_activeWorkspace is null)
        {
            StatusMessage = "No workspace is currently open.";
            return;
        }

        if (SelectedEnvironment is null)
        {
            StatusMessage = "No environment is selected.";
            return;
        }

        if (!Guid.TryParse(SelectedEnvironment.Id, out var environmentId))
        {
            StatusMessage = "Selected environment ID is invalid.";

            MessageBox.Show(
                "Selected environment ID is invalid.",
                "Deadbelt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        var owner = System.Windows.Application.Current.MainWindow;

        if (owner is null)
        {
            StatusMessage = "Unable to open edit environment dialog.";
            return;
        }

        var dialogResult = _editEnvironmentDialogService.ShowEditEnvironmentDialog(
            owner,
            SelectedEnvironment);

        if (!dialogResult.Confirmed)
        {
            StatusMessage = "Environment edit cancelled.";
            return;
        }

        StatusMessage = "Updating environment...";

        var result = await _environmentService.UpdateEnvironmentAsync(
            new UpdateEnvironmentRequest
            {
                WorkspacePath = _activeWorkspace.Path,
                EnvironmentId = environmentId,
                Name = dialogResult.Name,
                Description = dialogResult.Description,
                GameType = dialogResult.GameType
            });

        if (!result.Succeeded || result.Environment is null)
        {
            StatusMessage = "Failed to update environment.";

            MessageBox.Show(
                result.ErrorMessage ?? "Failed to update environment.",
                "Deadbelt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        ReplaceSelectedEnvironment(result.Environment);

        StatusMessage = "Environment updated.";
    }

    private async Task ArchiveEnvironmentAsync()
    {
        if (_activeWorkspace is null)
        {
            StatusMessage = "No workspace is currently open.";
            return;
        }

        if (SelectedEnvironment is null)
        {
            StatusMessage = "No environment is selected.";
            return;
        }

        if (!Guid.TryParse(SelectedEnvironment.Id, out var environmentId))
        {
            StatusMessage = "Selected environment ID is invalid.";

            MessageBox.Show(
                "Selected environment ID is invalid.",
                "Deadbelt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        var confirmation = MessageBox.Show(
            "Archive this environment?\n\nThis will mark the environment as archived but will not delete any files.",
            "Deadbelt",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            StatusMessage = "Environment archive cancelled.";
            return;
        }

        StatusMessage = "Archiving environment...";

        var result = await _environmentService.ArchiveEnvironmentAsync(
            new ArchiveEnvironmentRequest
            {
                WorkspacePath = _activeWorkspace.Path,
                EnvironmentId = environmentId
            });

        if (!result.Succeeded || result.Environment is null)
        {
            StatusMessage = "Failed to archive environment.";

            MessageBox.Show(
                result.ErrorMessage ?? "Failed to archive environment.",
                "Deadbelt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        ReplaceSelectedEnvironment(result.Environment);

        StatusMessage = "Environment archived.";
    }

    private async Task RestoreEnvironmentAsync()
    {
        if (_activeWorkspace is null)
        {
            StatusMessage = "No workspace is currently open.";
            return;
        }

        if (SelectedEnvironment is null)
        {
            StatusMessage = "No environment is selected.";
            return;
        }

        if (!Guid.TryParse(SelectedEnvironment.Id, out var environmentId))
        {
            StatusMessage = "Selected environment ID is invalid.";

            MessageBox.Show(
                "Selected environment ID is invalid.",
                "Deadbelt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        var confirmation = MessageBox.Show(
            "Restore this environment?\n\nThis will mark the environment as Draft and make it available for future workflows.",
            "Deadbelt",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            StatusMessage = "Environment restore cancelled.";
            return;
        }

        StatusMessage = "Restoring environment...";

        var result = await _environmentService.RestoreEnvironmentAsync(
            new RestoreEnvironmentRequest
            {
                WorkspacePath = _activeWorkspace.Path,
                EnvironmentId = environmentId
            });

        if (!result.Succeeded || result.Environment is null)
        {
            StatusMessage = "Failed to restore environment.";

            MessageBox.Show(
                result.ErrorMessage ?? "Failed to restore environment.",
                "Deadbelt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        ReplaceSelectedEnvironment(result.Environment);

        StatusMessage = "Environment restored.";
    }

    private void ApplyEnvironmentFilter(
        EnvironmentSummaryViewModel? preferredSelection = null)
    {
        var previousSelection = preferredSelection ?? SelectedEnvironment;

        Environments.Clear();

        var visibleEnvironments = _allEnvironments
            .Where(environment =>
                SelectedEnvironmentStatusFilter?.Matches(environment) ?? true)
            .ToArray();

        foreach (var environment in visibleEnvironments)
        {
            Environments.Add(environment);
        }

        if (previousSelection is not null)
        {
            SelectedEnvironment = Environments.FirstOrDefault(environment =>
                string.Equals(
                    environment.Id,
                    previousSelection.Id,
                    StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            SelectedEnvironment = Environments.FirstOrDefault();
        }

        if (SelectedEnvironment is null && Environments.Count > 0)
            SelectedEnvironment = Environments.FirstOrDefault();

        RefreshEnvironmentState();
    }

    private async Task LoadRecentWorkspacesAsync()
    {
        var recentWorkspaces = await _recentWorkspaceService.GetRecentWorkspacesAsync();

        RecentWorkspaces.Clear();

        foreach (var recentWorkspace in recentWorkspaces)
        {
            var recentWorkspaceSummary =
                RecentWorkspaceSummaryViewModel.FromRecentWorkspace(recentWorkspace);

            recentWorkspaceSummary.UpdateActiveState(_activeWorkspace?.Path);

            RecentWorkspaces.Add(recentWorkspaceSummary);
        }

        SelectedRecentWorkspace = RecentWorkspaces.FirstOrDefault();

        RefreshRecentWorkspaceState();
    }

    private async Task RecordActiveWorkspaceAsRecentAsync(Workspace workspace)
    {
        await _recentWorkspaceService.RecordWorkspaceAsync(workspace);

        await LoadRecentWorkspacesAsync();
    }

    private void RefreshRecentWorkspaceActiveState()
    {
        foreach (var recentWorkspace in RecentWorkspaces)
        {
            recentWorkspace.UpdateActiveState(_activeWorkspace?.Path);
        }

        OpenRecentWorkspaceCommand.RaiseCanExecuteChanged();
    }

    private void RefreshRecentWorkspaceState()
    {
        OnPropertyChanged(nameof(HasRecentWorkspaces));
        OnPropertyChanged(nameof(CanOpenSelectedRecentWorkspace));
        OnPropertyChanged(nameof(CanRemoveSelectedRecentWorkspace));

        OpenRecentWorkspaceCommand.RaiseCanExecuteChanged();
        RemoveRecentWorkspaceCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadActiveWorkspaceEnvironmentsAsync()
    {
        if (_activeWorkspace is null)
            return;

        _allEnvironments.Clear();
        Environments.Clear();
        SelectedEnvironment = null;

        var environments = await _environmentService.LoadByWorkspaceAsync(
            _activeWorkspace.Path);

        foreach (var environment in environments)
        {
            _allEnvironments.Add(
                EnvironmentSummaryViewModel.FromEnvironment(environment));
        }

        ApplyEnvironmentFilter();
    }

    private void ReplaceSelectedEnvironment(Deadbelt.Domain.Environments.Environment environment)
    {
        var updatedSummary = EnvironmentSummaryViewModel.FromEnvironment(environment);

        var existingIndex = _allEnvironments.FindIndex(existingEnvironment =>
            string.Equals(
                existingEnvironment.Id,
                updatedSummary.Id,
                StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
            _allEnvironments[existingIndex] = updatedSummary;
        else
            _allEnvironments.Add(updatedSummary);

        ApplyEnvironmentFilter(updatedSummary);
    }

    private void SetActiveWorkspace(Workspace workspace)
    {
        _activeWorkspace = workspace;

        _allEnvironments.Clear();
        Environments.Clear();
        SelectedEnvironment = null;

        WorkspaceStatus = $"Workspace: {workspace.Name}";
        WelcomeMessage = $"Active workspace location: {workspace.Path}";

        NavigateTo(OverviewSection);

        OnPropertyChanged(nameof(IsWorkspaceOpen));
        OnPropertyChanged(nameof(ActiveWorkspaceName));
        OnPropertyChanged(nameof(ActiveWorkspacePath));
        OnPropertyChanged(nameof(ActiveWorkspaceVersion));

        RefreshEnvironmentState();
        RefreshRecentWorkspaceActiveState();

        CreateEnvironmentCommand.RaiseCanExecuteChanged();
        EditEnvironmentCommand.RaiseCanExecuteChanged();
        ArchiveEnvironmentCommand.RaiseCanExecuteChanged();
        RestoreEnvironmentCommand.RaiseCanExecuteChanged();
    }

    private void RefreshEnvironmentState()
    {
        OnPropertyChanged(nameof(EnvironmentCount));
        OnPropertyChanged(nameof(HasEnvironments));
        OnPropertyChanged(nameof(HasVisibleEnvironments));
        OnPropertyChanged(nameof(HasSelectedEnvironment));
        OnPropertyChanged(nameof(CanArchiveSelectedEnvironment));
        OnPropertyChanged(nameof(CanRestoreSelectedEnvironment));

        CreateEnvironmentCommand.RaiseCanExecuteChanged();
        EditEnvironmentCommand.RaiseCanExecuteChanged();
        ArchiveEnvironmentCommand.RaiseCanExecuteChanged();
        RestoreEnvironmentCommand.RaiseCanExecuteChanged();
    }


    private void NavigateTo(string section)
    {
        SelectedNavigationSection = section;
        StatusMessage = $"{section} selected.";
    }
}
