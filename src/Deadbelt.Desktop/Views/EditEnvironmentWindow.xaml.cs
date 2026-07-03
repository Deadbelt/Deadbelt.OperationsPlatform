using System.Windows;
using Deadbelt.Desktop.ViewModels;

namespace Deadbelt.Desktop.Views;

public partial class EditEnvironmentWindow : Window
{
    private readonly EditEnvironmentViewModel _viewModel;

    public EditEnvironmentWindow(EditEnvironmentViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsValid())
            return;

        DialogResult = true;
        Close();
    }
}