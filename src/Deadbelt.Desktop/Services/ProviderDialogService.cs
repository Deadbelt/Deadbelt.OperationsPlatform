using System.Windows;
using Deadbelt.Desktop.ViewModels;
using Deadbelt.Desktop.Views;

namespace Deadbelt.Desktop.Services;

public sealed class ProviderDialogService : IProviderDialogService
{
    public CreateProviderDialogResult ShowCreateProviderDialog(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var viewModel = new CreateProviderDialogViewModel();

        var dialog = new CreateProviderDialog
        {
            Owner = owner,
            DataContext = viewModel
        };

        var result = dialog.ShowDialog();

        if (result != true)
            return CreateProviderDialogResult.Cancelled();

        return CreateProviderDialogResult.ConfirmedResult(
            viewModel.Name,
            viewModel.SelectedProviderType);
    }
}
