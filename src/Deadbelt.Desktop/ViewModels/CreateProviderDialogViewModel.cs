using System.Collections.ObjectModel;
using Deadbelt.Desktop.MVVM;
using Deadbelt.Domain.Providers;

namespace Deadbelt.Desktop.ViewModels;

public sealed class CreateProviderDialogViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _errorMessage = string.Empty;
    private ProviderTypeOptionViewModel? _selectedProviderTypeOption;

    public CreateProviderDialogViewModel()
    {
        foreach (var providerType in ProviderTypeOptionViewModel.CreateDefaultOptions())
        {
            ProviderTypes.Add(providerType);
        }

        SelectedProviderTypeOption = ProviderTypes.FirstOrDefault();
    }

    public ObservableCollection<ProviderTypeOptionViewModel> ProviderTypes { get; } = [];

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                ClearError();
        }
    }

    public ProviderTypeOptionViewModel? SelectedProviderTypeOption
    {
        get => _selectedProviderTypeOption;
        set
        {
            if (SetProperty(ref _selectedProviderTypeOption, value))
                ClearError();
        }
    }

    public ProviderType SelectedProviderType =>
        SelectedProviderTypeOption?.ProviderType ?? ProviderType.Unknown;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Provider name is required.";
            return false;
        }

        if (SelectedProviderType == ProviderType.Unknown)
        {
            ErrorMessage = "Provider type is required.";
            return false;
        }

        Name = Name.Trim();
        ClearError();

        return true;
    }

    private void ClearError()
    {
        if (!string.IsNullOrWhiteSpace(ErrorMessage))
            ErrorMessage = string.Empty;
    }
}
