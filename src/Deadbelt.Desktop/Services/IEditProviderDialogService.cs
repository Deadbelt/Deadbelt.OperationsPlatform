using System.Windows;
using Deadbelt.Desktop.ViewModels;

namespace Deadbelt.Desktop.Services;

public interface IEditProviderDialogService
{
    EditProviderDialogResult ShowEditProviderDialog(
        Window owner,
        ProviderSummaryViewModel provider);
}
