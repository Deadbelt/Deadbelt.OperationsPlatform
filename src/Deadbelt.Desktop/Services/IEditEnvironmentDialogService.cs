using System.Windows;
using Deadbelt.Desktop.ViewModels;

namespace Deadbelt.Desktop.Services;

public interface IEditEnvironmentDialogService
{
    EditEnvironmentDialogResult ShowEditEnvironmentDialog(
        Window owner,
        EnvironmentSummaryViewModel environment);
}