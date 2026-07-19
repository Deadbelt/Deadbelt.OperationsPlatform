using System.Windows;
using Deadbelt.Desktop.ViewModels;

namespace Deadbelt.Desktop.Views;

public partial class CreateProviderDialog : Window
{
    public CreateProviderDialog()
    {
        InitializeComponent();
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CreateProviderDialogViewModel viewModel)
            return;

        if (!viewModel.Validate())
            return;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
