using System.Windows;
using Deadbelt.Application.Common;
using Deadbelt.Desktop.ViewModels;
using Deadbelt.Desktop.Views;
using Microsoft.Win32;

namespace Deadbelt.Desktop.Services;

public sealed class WorkspaceDialogService : IWorkspaceDialogService
{
    private readonly IPathInspector _pathInspector;

    public WorkspaceDialogService(IPathInspector pathInspector)
    {
        _pathInspector = pathInspector;
    }

    public WorkspaceDialogResult ShowCreateWorkspaceDialog(Window owner)
    {
        var viewModel = new CreateWorkspaceViewModel(_pathInspector);

        var window = new CreateWorkspaceWindow(viewModel)
        {
            Owner = owner
        };

        var result = window.ShowDialog();

        if (result != true)
            return WorkspaceDialogResult.Cancelled();

        return WorkspaceDialogResult.Success(
            viewModel.WorkspaceName,
            viewModel.FolderPath,
            viewModel.Description);
    }

    public string? ShowOpenWorkspaceDialog(Window owner)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Deadbelt Workspace Folder"
        };

        return dialog.ShowDialog(owner) == true
            ? dialog.FolderName
            : null;
    }
}
