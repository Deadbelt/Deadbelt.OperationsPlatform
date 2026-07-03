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
    private readonly IEnvironmentService _environmentService;
    private readonly IEnvironmentDialogService _environmentDialogService;
    private readonly IEditEnvironmentDialogService _editEnvironmentDialogService;

    private Workspace? _activeWorkspace;
    private EnvironmentSummaryViewModel? _selectedEnvironment;

    private string _selectedNavigationSection = OverviewSection;
    private string _workspaceStatus = "Workspace: None";
    private string _welcomeMessage = "No workspace is currently open.";
    private string _statusMessage = "Ready";

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        IWorkspaceDialogService workspaceDialogService,
        IEnvironmentService environmentService,
        IEnvironmentDialogService environmentDialogService,
        IEditEnvironmentDialogService editEnvironmentDialogService)
    {
        _workspaceService = workspaceService;
        _workspaceDialogService = workspaceDialogService;
        _environmentService = environmentService;
        _environmentDialogService = environmentDialogService;
        _editEnvironmentDialogService = editEnvironmentDialogService;

        CreateWorkspaceCommand = new AsyncRelayCommand(CreateWorkspaceAsync);
        OpenWorkspaceCommand = new AsyncRelayCommand(OpenWorkspaceAsync);

        CreateEnvironmentCommand = new AsyncRelayCommand(
            CreateEnvironmentAsync,
            () => IsWorkspaceOpen);

        EditEnvironmentCommand = new AsyncRelayCommand(
            EditEnvironmentAsync,
            () => IsWorkspaceOpen && SelectedEnvironment is not null);

        NavigateOverviewCommand = new RelayCommand(() => NavigateTo(OverviewSection));
        NavigateEnvironmentsCommand = new RelayCommand(() => NavigateTo(EnvironmentsSection));
        NavigateProvidersCommand = new RelayCommand(() => NavigateTo(ProvidersSection));
        NavigateJobsCommand = new RelayCommand(() => NavigateTo(JobsSection));
        NavigateSettingsCommand = new RelayCommand(() => NavigateTo(SettingsSection));
    }

    public string ApplicationName => "DEADBELT";

    public string ApplicationSubtitle => "Operations Platform";

    public bool IsWorkspaceOpen => _activeWorkspace is not null;

    public string ActiveWorkspaceName => _activeWorkspace?.Name ?? "None";

    public string ActiveWorkspacePath => _activeWorkspace?.Path ?? string.Empty;

    public string ActiveWorkspaceVersion => _activeWorkspace?.Version ?? string.Empty;

    public ObservableCollection<EnvironmentSummaryViewModel> Environments { get; } = [];

    public int EnvironmentCount => Environments.Count;

    public bool HasEnvironments => Environments.Count > 0;

    public EnvironmentSummaryViewModel? SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            if (SetProperty(ref _selectedEnvironment, value))
            {
                OnPropertyChanged(nameof(HasSelectedEnvironment));
                EditEnvironmentCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedEnvironment => SelectedEnvironment is not null;

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

    public AsyncRelayCommand CreateEnvironmentCommand { get; }

    public AsyncRelayCommand EditEnvironmentCommand { get; }

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

            return;
        }

        SetActiveWorkspace(result.Workspace);

        await LoadActiveWorkspaceEnvironmentsAsync();

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

        Environments.Add(environmentSummary);
        SelectedEnvironment = environmentSummary;

        RefreshEnvironmentState();

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

        var updatedSummary = EnvironmentSummaryViewModel.FromEnvironment(result.Environment);

        var selectedIndex = Environments.IndexOf(SelectedEnvironment);

        if (selectedIndex >= 0)
            Environments[selectedIndex] = updatedSummary;

        SelectedEnvironment = updatedSummary;

        RefreshEnvironmentState();

        StatusMessage = "Environment updated.";
    }

    private async Task LoadActiveWorkspaceEnvironmentsAsync()
    {
        if (_activeWorkspace is null)
            return;

        Environments.Clear();
        SelectedEnvironment = null;

        var environments = await _environmentService.LoadByWorkspaceAsync(
            _activeWorkspace.Path);

        foreach (var environment in environments)
        {
            Environments.Add(
                EnvironmentSummaryViewModel.FromEnvironment(environment));
        }

        SelectedEnvironment = Environments.FirstOrDefault();

        RefreshEnvironmentState();
    }

    private void SetActiveWorkspace(Workspace workspace)
    {
        _activeWorkspace = workspace;

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

        CreateEnvironmentCommand.RaiseCanExecuteChanged();
        EditEnvironmentCommand.RaiseCanExecuteChanged();
    }

    private void RefreshEnvironmentState()
    {
        OnPropertyChanged(nameof(EnvironmentCount));
        OnPropertyChanged(nameof(HasEnvironments));
        OnPropertyChanged(nameof(HasSelectedEnvironment));

        CreateEnvironmentCommand.RaiseCanExecuteChanged();
        EditEnvironmentCommand.RaiseCanExecuteChanged();
    }

    private void NavigateTo(string section)
    {
        SelectedNavigationSection = section;
        StatusMessage = $"{section} selected.";
    }
}
