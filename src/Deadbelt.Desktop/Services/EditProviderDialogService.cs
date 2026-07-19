using System.Windows;
using Deadbelt.Desktop.ViewModels;
using Deadbelt.Desktop.Views;

namespace Deadbelt.Desktop.Services;

public sealed class EditProviderDialogService : IEditProviderDialogService
{
    public EditProviderDialogResult ShowEditProviderDialog(
        Window owner,
        ProviderSummaryViewModel provider)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(provider);

        var viewModel = new EditProviderDialogViewModel(provider);

        var dialog = new EditProviderDialog
        {
            Owner = owner,
            DataContext = viewModel
        };

        var result = dialog.ShowDialog();

        if (result != true)
            return EditProviderDialogResult.Cancelled();

        return EditProviderDialogResult.ConfirmedResult(
            viewModel.Name,
            viewModel.SelectedProviderType);
    }
}
