using Deadbelt.Application.Workspaces;
using Deadbelt.Desktop.Services;
using Deadbelt.Desktop.ViewModels;
using Deadbelt.Desktop.Views;
using Deadbelt.Infrastructure.Workspaces;
using Deadbelt.Application.Environments;
using Deadbelt.Infrastructure.Environments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Deadbelt.Desktop.Bootstrap;

public static class Bootstrapper
{
    public static IHost BuildHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IWorkspaceStore, JsonWorkspaceStore>();
                services.AddSingleton<IWorkspaceService, WorkspaceService>();
                services.AddSingleton<IWorkspaceDialogService, WorkspaceDialogService>();

                services.AddSingleton<IEnvironmentStore, JsonEnvironmentStore>();
                services.AddSingleton<IEnvironmentService, EnvironmentService>();

                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
            })
            .Build();
    }
}
