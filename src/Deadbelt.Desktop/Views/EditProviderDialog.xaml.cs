using System.Windows;
using Deadbelt.Desktop.ViewModels;

namespace Deadbelt.Desktop.Views;

public partial class EditProviderDialog : Window
{
    public EditProviderDialog()
    {
        InitializeComponent();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EditProviderDialogViewModel viewModel)
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
