using Microsoft.Win32;

namespace Deadbelt.Desktop.Services;

public sealed class DoctorPathDialogService : IDoctorPathDialogService
{
    public string? SelectFolder(
        string title,
        string? initialPath = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
            dialog.InitialDirectory = initialPath;

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }

    public string? SelectFile(
        string title,
        string filter,
        string? initialPath = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
            dialog.InitialDirectory = initialPath;

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }
}
