namespace Deadbelt.Desktop.Services;

public interface IDoctorPathDialogService
{
    string? SelectFolder(
        string title,
        string? initialPath = null);

    string? SelectFile(
        string title,
        string filter,
        string? initialPath = null);
}
