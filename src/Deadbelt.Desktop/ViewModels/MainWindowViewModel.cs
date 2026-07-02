using System.Windows;
using System.Windows.Input;
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

    private Workspace? _activeWorkspace;

    private string _selectedNavigationSection = OverviewSection;
    private string _workspaceStatus = "Workspace: None";
    private string _welcomeMessage = "No workspace is currently open.";
    private string _statusMessage = "Ready";

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        IWorkspaceDialogService workspaceDialogService)
    {
        _workspaceService = workspaceService;
        _workspaceDialogService = workspaceDialogService;

        CreateWorkspaceCommand = new AsyncRelayCommand(CreateWorkspaceAsync);
        OpenWorkspaceCommand = new AsyncRelayCommand(OpenWorkspaceAsync);

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

        SetActiveWorkspace(result.Workspace, "Workspace created.");
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

        SetActiveWorkspace(result.Workspace, "Workspace opened.");
    }

    private void SetActiveWorkspace(Workspace workspace, string statusMessage)
    {
        _activeWorkspace = workspace;

        WorkspaceStatus = $"Workspace: {workspace.Name}";
        WelcomeMessage = $"Active workspace location: {workspace.Path}";
        StatusMessage = statusMessage;

        NavigateTo(OverviewSection);

        OnPropertyChanged(nameof(IsWorkspaceOpen));
        OnPropertyChanged(nameof(ActiveWorkspaceName));
        OnPropertyChanged(nameof(ActiveWorkspacePath));
        OnPropertyChanged(nameof(ActiveWorkspaceVersion));
    }

    private void NavigateTo(string section)
    {
        SelectedNavigationSection = section;
        StatusMessage = $"{section} selected.";
    }
}
