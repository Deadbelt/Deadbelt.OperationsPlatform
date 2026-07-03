using Deadbelt.Desktop.MVVM;
using Deadbelt.Domain.Environments;

namespace Deadbelt.Desktop.ViewModels;

public sealed class EditEnvironmentViewModel : ViewModelBase
{
    private string _environmentName;
    private string? _description;
    private GameType _selectedGameType;
    private string _validationMessage = string.Empty;

    public EditEnvironmentViewModel(EnvironmentSummaryViewModel environment)
    {
        EnvironmentId = environment.Id;
        EnvironmentPath = environment.EnvironmentPath;

        _environmentName = environment.Name;
        _description = environment.Description;
        _selectedGameType = environment.GameType;

        AvailableGameTypes = Enum
            .GetValues<GameType>()
            .Where(gameType => gameType != GameType.Unknown)
            .ToArray();

        SaveCommand = new RelayCommand(
            execute: () => { },
            canExecute: CanSave);

        Validate();
    }

    public string EnvironmentId { get; }

    public string EnvironmentPath { get; }

    public IReadOnlyList<GameType> AvailableGameTypes { get; }

    public string EnvironmentName
    {
        get => _environmentName;
        set
        {
            if (SetProperty(ref _environmentName, value))
                Validate();
        }
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public GameType SelectedGameType
    {
        get => _selectedGameType;
        set
        {
            if (SetProperty(ref _selectedGameType, value))
                Validate();
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public RelayCommand SaveCommand { get; }

    public bool IsValid()
    {
        Validate();
        return string.IsNullOrWhiteSpace(ValidationMessage);
    }

    private bool CanSave()
    {
        return !string.IsNullOrWhiteSpace(EnvironmentName)
            && SelectedGameType != GameType.Unknown;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(EnvironmentName))
        {
            ValidationMessage = "Environment name is required.";
            SaveCommand.RaiseCanExecuteChanged();
            return;
        }

        if (SelectedGameType == GameType.Unknown)
        {
            ValidationMessage = "Environment game type is required.";
            SaveCommand.RaiseCanExecuteChanged();
            return;
        }

        ValidationMessage = string.Empty;
        SaveCommand.RaiseCanExecuteChanged();
    }
}