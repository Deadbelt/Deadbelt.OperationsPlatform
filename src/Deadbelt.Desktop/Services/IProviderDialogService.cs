using System.Windows;

namespace Deadbelt.Desktop.Services;

public interface IProviderDialogService
{
    CreateProviderDialogResult ShowCreateProviderDialog(Window owner);
}
