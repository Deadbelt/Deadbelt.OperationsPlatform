using Deadbelt.Application.Workspaces;
using Deadbelt.Desktop.MVVM;

namespace Deadbelt.Desktop.ViewModels;

public sealed class RecentWorkspaceSummaryViewModel : ViewModelBase
{
    private bool _isActive;

    private RecentWorkspaceSummaryViewModel(
        string name,
        string path,
        DateTime lastOpenedUtc)
    {
        Name = name;
        Path = path;
        LastOpenedUtc = lastOpenedUtc;
    }

    public string Name { get; }

    public string Path { get; }

    public DateTime LastOpenedUtc { get; }

    public string LastOpenedDisplay => LastOpenedUtc.ToString("u");

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
                OnPropertyChanged(nameof(ActionText));
        }
    }

    public string ActionText => IsActive
        ? "Active"
        : "Select to open";

    public void UpdateActiveState(string? activeWorkspacePath)
    {
        IsActive = !string.IsNullOrWhiteSpace(activeWorkspacePath)
            && string.Equals(
                Path,
                activeWorkspacePath,
                StringComparison.OrdinalIgnoreCase);
    }

    public static RecentWorkspaceSummaryViewModel FromRecentWorkspace(
        RecentWorkspace recentWorkspace)
    {
        return new RecentWorkspaceSummaryViewModel(
            recentWorkspace.Name,
            recentWorkspace.Path,
            recentWorkspace.LastOpenedUtc);
    }
}