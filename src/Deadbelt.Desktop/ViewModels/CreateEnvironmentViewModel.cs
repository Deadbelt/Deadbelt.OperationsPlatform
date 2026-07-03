using Deadbelt.Desktop.MVVM;
using Deadbelt.Domain.Environments;

namespace Deadbelt.Desktop.ViewModels;

public sealed class CreateEnvironmentViewModel : ViewModelBase
{
    private string _environmentName = string.Empty;
    private string? _description;
    private GameType _selectedGameType = GameType.DayZ;
    private string _validationMessage = "Environment name is required.";

    public CreateEnvironmentViewModel()
    {
        AvailableGameTypes = Enum
            .GetValues<GameType>()
            .Where(gameType => gameType != GameType.Unknown)
            .ToArray();

        CreateCommand = new RelayCommand(
            execute: () => { },
            canExecute: CanCreate);
    }

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

    public RelayCommand CreateCommand { get; }

    public bool IsValid()
    {
        Validate();
        return string.IsNullOrWhiteSpace(ValidationMessage);
    }

    private bool CanCreate()
    {
        return !string.IsNullOrWhiteSpace(EnvironmentName)
            && SelectedGameType != GameType.Unknown;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(EnvironmentName))
        {
            ValidationMessage = "Environment name is required.";
            CreateCommand.RaiseCanExecuteChanged();
            return;
        }

        if (SelectedGameType == GameType.Unknown)
        {
            ValidationMessage = "Environment game type is required.";
            CreateCommand.RaiseCanExecuteChanged();
            return;
        }

        ValidationMessage = string.Empty;
        CreateCommand.RaiseCanExecuteChanged();
    }
}