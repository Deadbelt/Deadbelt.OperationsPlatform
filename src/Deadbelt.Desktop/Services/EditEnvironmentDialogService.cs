using System.Windows;
using Deadbelt.Desktop.ViewModels;
using Deadbelt.Desktop.Views;

namespace Deadbelt.Desktop.Services;

public sealed class EditEnvironmentDialogService : IEditEnvironmentDialogService
{
    public EditEnvironmentDialogResult ShowEditEnvironmentDialog(
        Window owner,
        EnvironmentSummaryViewModel environment)
    {
        var viewModel = new EditEnvironmentViewModel(environment);

        var window = new EditEnvironmentWindow(viewModel)
        {
            Owner = owner
        };

        var result = window.ShowDialog();

        if (result != true)
            return EditEnvironmentDialogResult.Cancelled();

        return EditEnvironmentDialogResult.Success(
            viewModel.EnvironmentName,
            viewModel.Description,
            viewModel.SelectedGameType);
    }
}