using System.Collections.ObjectModel;
using Deadbelt.Application.Doctor;
using Deadbelt.Desktop.MVVM;
using Deadbelt.Desktop.Services;
using Deadbelt.Domain.Doctor;
using Deadbelt.Domain.Environments;

namespace Deadbelt.Desktop.ViewModels;

public sealed class DoctorViewModel : ViewModelBase
{
    private readonly IDoctorService _doctorService;
    private readonly IDoctorPathDialogService _pathDialogService;

    private DoctorEnvironmentOptionViewModel? _selectedEnvironment;
    private CancellationTokenSource? _scanCancellation;
    private string? _workspacePath;
    private string _targetRootPath = string.Empty;
    private string _startupFilePath = string.Empty;
    private string _configurationFilePath = string.Empty;
    private string _statusMessage = "Select an Environment and local DayZ server root.";
    private string _scanStatus = string.Empty;
    private string _duration = string.Empty;
    private string _inventorySummary = string.Empty;
    private int _informationCount;
    private int _warningCount;
    private int _errorCount;
    private int _contextVersion;
    private bool _isScanning;

    public DoctorViewModel(
        IDoctorService doctorService,
        IDoctorPathDialogService pathDialogService)
    {
        _doctorService = doctorService
            ?? throw new ArgumentNullException(nameof(doctorService));
        _pathDialogService = pathDialogService
            ?? throw new ArgumentNullException(nameof(pathDialogService));

        ScanCommand = new AsyncRelayCommand(
            ScanAsync,
            CanScan);
        CancelCommand = new RelayCommand(
            CancelScan,
            () => IsScanning);
        BrowseTargetRootCommand = new RelayCommand(BrowseTargetRoot);
        BrowseStartupFileCommand = new RelayCommand(BrowseStartupFile);
        BrowseConfigurationFileCommand = new RelayCommand(BrowseConfigurationFile);
        ClearStartupFileCommand = new RelayCommand(
            () => StartupFilePath = string.Empty,
            () => !string.IsNullOrEmpty(StartupFilePath));
        ClearConfigurationFileCommand = new RelayCommand(
            () => ConfigurationFilePath = string.Empty,
            () => !string.IsNullOrEmpty(ConfigurationFilePath));
    }

    public ObservableCollection<DoctorEnvironmentOptionViewModel> Environments { get; } = [];

    public ObservableCollection<DoctorFindingViewModel> Findings { get; } = [];

    public DoctorEnvironmentOptionViewModel? SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            if (!SetProperty(ref _selectedEnvironment, value))
                return;

            InvalidateResult("Environment selection changed. Run a new scan.");
            ScanCommand.RaiseCanExecuteChanged();
            RaiseClearCommandStates();
        }
    }

    public string TargetRootPath
    {
        get => _targetRootPath;
        set
        {
            if (!SetProperty(ref _targetRootPath, value ?? string.Empty))
                return;

            InvalidateResult("Target selection changed. Run a new scan.");
            ScanCommand.RaiseCanExecuteChanged();
        }
    }

    public string StartupFilePath
    {
        get => _startupFilePath;
        set
        {
            if (!SetProperty(ref _startupFilePath, value ?? string.Empty))
                return;

            InvalidateResult("Startup selection changed. Run a new scan.");
            ClearStartupFileCommand.RaiseCanExecuteChanged();
        }
    }

    public string ConfigurationFilePath
    {
        get => _configurationFilePath;
        set
        {
            if (!SetProperty(ref _configurationFilePath, value ?? string.Empty))
                return;

            InvalidateResult("Configuration selection changed. Run a new scan.");
            ClearConfigurationFileCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ScanStatus
    {
        get => _scanStatus;
        private set => SetProperty(ref _scanStatus, value);
    }

    public string Duration
    {
        get => _duration;
        private set => SetProperty(ref _duration, value);
    }

    public string InventorySummary
    {
        get => _inventorySummary;
        private set => SetProperty(ref _inventorySummary, value);
    }

    public int InformationCount
    {
        get => _informationCount;
        private set => SetProperty(ref _informationCount, value);
    }

    public int WarningCount
    {
        get => _warningCount;
        private set => SetProperty(ref _warningCount, value);
    }

    public int ErrorCount
    {
        get => _errorCount;
        private set => SetProperty(ref _errorCount, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (!SetProperty(ref _isScanning, value))
                return;

            OnPropertyChanged(nameof(IsNotScanning));
            ScanCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsNotScanning => !IsScanning;

    public bool HasResults => !string.IsNullOrEmpty(ScanStatus);

    public AsyncRelayCommand ScanCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand BrowseTargetRootCommand { get; }

    public RelayCommand BrowseStartupFileCommand { get; }

    public RelayCommand BrowseConfigurationFileCommand { get; }

    public RelayCommand ClearStartupFileCommand { get; }

    public RelayCommand ClearConfigurationFileCommand { get; }

    public void UpdateContext(
        string workspacePath,
        IEnumerable<EnvironmentSummaryViewModel> environments)
    {
        ArgumentNullException.ThrowIfNull(environments);

        CancelActiveScan();
        IsScanning = false;
        _contextVersion++;
        var workspaceChanged = !string.Equals(
            _workspacePath,
            workspacePath,
            StringComparison.OrdinalIgnoreCase);
        _workspacePath = workspacePath;
        var selectedId = SelectedEnvironment?.Id;

        if (workspaceChanged)
        {
            _targetRootPath = string.Empty;
            _startupFilePath = string.Empty;
            _configurationFilePath = string.Empty;
            OnPropertyChanged(nameof(TargetRootPath));
            OnPropertyChanged(nameof(StartupFilePath));
            OnPropertyChanged(nameof(ConfigurationFilePath));
        }

        Environments.Clear();

        foreach (var environment in environments)
        {
            if (!Guid.TryParse(environment.Id, out var id))
                continue;

            Environments.Add(
                new DoctorEnvironmentOptionViewModel(
                    EnvironmentId.From(id),
                    environment.Name,
                    environment.GameType));
        }

        _selectedEnvironment = selectedId is null
            ? Environments.FirstOrDefault()
            : Environments.FirstOrDefault(candidate => candidate.Id == selectedId)
                ?? Environments.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedEnvironment));

        ClearResult();
        StatusMessage = Environments.Count == 0
            ? "No Environment is available in the active Workspace."
            : "Select the local DayZ server root, then run Doctor.";
        ScanCommand.RaiseCanExecuteChanged();
        RaiseClearCommandStates();
    }

    public void SelectEnvironment(string? environmentId)
    {
        if (!Guid.TryParse(environmentId, out var parsedId))
            return;

        SelectedEnvironment = Environments.FirstOrDefault(
            environment => environment.Id.Value == parsedId);
    }

    public void ClearContext()
    {
        CancelActiveScan();
        IsScanning = false;
        _contextVersion++;
        _workspacePath = null;
        Environments.Clear();
        _selectedEnvironment = null;
        OnPropertyChanged(nameof(SelectedEnvironment));
        _targetRootPath = string.Empty;
        _startupFilePath = string.Empty;
        _configurationFilePath = string.Empty;
        OnPropertyChanged(nameof(TargetRootPath));
        OnPropertyChanged(nameof(StartupFilePath));
        OnPropertyChanged(nameof(ConfigurationFilePath));
        ClearResult();
        StatusMessage = "Open a Workspace to use Doctor.";
        ScanCommand.RaiseCanExecuteChanged();
        RaiseClearCommandStates();
    }

    private bool CanScan()
    {
        return !IsScanning
            && !string.IsNullOrWhiteSpace(_workspacePath)
            && SelectedEnvironment is not null
            && !string.IsNullOrWhiteSpace(TargetRootPath);
    }

    private async Task ScanAsync()
    {
        var environment = SelectedEnvironment;

        if (environment is null || string.IsNullOrWhiteSpace(TargetRootPath))
            return;

        ClearResult();
        StatusMessage = "Scanning the selected DayZ server without modifying it...";
        IsScanning = true;

        var contextVersion = _contextVersion;
        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;

        try
        {
            var result = await _doctorService.ScanAsync(
                new DoctorScanRequest(
                    _workspacePath!,
                    environment.Id,
                    environment.Name,
                    environment.GameType,
                    TargetRootPath,
                    NullIfWhiteSpace(StartupFilePath),
                    NullIfWhiteSpace(ConfigurationFilePath)),
                cancellation.Token);

            if (contextVersion != _contextVersion
                || cancellation.IsCancellationRequested
                    && result.Status != DoctorScanStatus.Cancelled)
            {
                return;
            }

            ApplyResult(result);
        }
        finally
        {
            if (ReferenceEquals(_scanCancellation, cancellation))
                _scanCancellation = null;

            cancellation.Dispose();

            if (contextVersion == _contextVersion)
                IsScanning = false;
        }
    }

    private void ApplyResult(DoctorScanResult result)
    {
        ScanStatus = result.Status.ToString();
        Duration = $"{result.Duration.TotalMilliseconds:0} ms";
        InformationCount = result.InformationCount;
        WarningCount = result.WarningCount;
        ErrorCount = result.ErrorCount;

        foreach (var finding in result.Findings)
            Findings.Add(DoctorFindingViewModel.FromFinding(finding));

        InventorySummary = CreateInventorySummary(result.Inventory);
        StatusMessage = result.Status switch
        {
            DoctorScanStatus.Completed => "Doctor scan completed.",
            DoctorScanStatus.Cancelled => "Doctor scan cancelled. Partial state was discarded.",
            _ => "Doctor scan failed safely."
        };
        OnPropertyChanged(nameof(HasResults));
    }

    private void BrowseTargetRoot()
    {
        var selected = _pathDialogService.SelectFolder(
            "Select the local DayZ server root",
            NullIfWhiteSpace(TargetRootPath));

        if (selected is not null)
            TargetRootPath = selected;
    }

    private void BrowseStartupFile()
    {
        var selected = _pathDialogService.SelectFile(
            "Select the DayZ startup script",
            "Supported startup scripts (*.bat;*.cmd;*.ps1)|*.bat;*.cmd;*.ps1|All files (*.*)|*.*",
            NullIfWhiteSpace(TargetRootPath));

        if (selected is not null)
            StartupFilePath = selected;
    }

    private void BrowseConfigurationFile()
    {
        var selected = _pathDialogService.SelectFile(
            "Select the DayZ configuration override",
            "DayZ configuration (*.cfg)|*.cfg|All files (*.*)|*.*",
            NullIfWhiteSpace(TargetRootPath));

        if (selected is not null)
            ConfigurationFilePath = selected;
    }

    private void CancelScan()
    {
        _scanCancellation?.Cancel();
        StatusMessage = "Cancelling Doctor scan...";
    }

    private void InvalidateResult(string message)
    {
        CancelActiveScan();
        IsScanning = false;
        _contextVersion++;
        ClearResult();
        StatusMessage = message;
    }

    private void CancelActiveScan()
    {
        _scanCancellation?.Cancel();
    }

    private void ClearResult()
    {
        Findings.Clear();
        ScanStatus = string.Empty;
        Duration = string.Empty;
        InventorySummary = string.Empty;
        InformationCount = 0;
        WarningCount = 0;
        ErrorCount = 0;
        OnPropertyChanged(nameof(HasResults));
    }

    private static string CreateInventorySummary(DoctorInventory? inventory)
    {
        if (inventory is null)
            return string.Empty;

        return string.Join(
            System.Environment.NewLine,
            $"Root: {inventory.TargetRootPath}",
            $"Executable: {inventory.ExecutablePath ?? "Not found"}",
            $"Startup: {inventory.SelectedStartupPath ?? "Unresolved"}",
            $"Configuration: {inventory.ActiveConfigurationPath ?? "Unresolved"}",
            $"Port: {inventory.LaunchArguments.GetValueOrDefault("port", "Unresolved")}",
            $"Mission: {inventory.MissionTemplate ?? "Unresolved"}",
            $"Client mods: {inventory.ClientMods.Count}",
            $"Server mods: {inventory.ServerMods.Count}",
            $"Global keys: {inventory.GlobalKeys.Count}",
            $"Profiles paths: {inventory.ProfilePaths.Count}",
            $"Storage paths: {inventory.StoragePaths.Count}",
            $"Log files: {inventory.LogFiles.Count}");
    }

    private static string? NullIfWhiteSpace(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private void RaiseClearCommandStates()
    {
        ClearStartupFileCommand.RaiseCanExecuteChanged();
        ClearConfigurationFileCommand.RaiseCanExecuteChanged();
    }
}
