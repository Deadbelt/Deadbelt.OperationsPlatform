using Deadbelt.Application.Environments;
using Deadbelt.Application.Providers;
using Deadbelt.Application.Workspaces;
using Deadbelt.Desktop.Services;
using Deadbelt.Desktop.ViewModels;
using Deadbelt.Desktop.Views;
using Deadbelt.Infrastructure.Environments;
using Deadbelt.Infrastructure.Providers;
using Deadbelt.Infrastructure.Workspaces;
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
                services.AddSingleton<IRecentWorkspaceStore, JsonRecentWorkspaceStore>();
                services.AddSingleton<IRecentWorkspaceService, RecentWorkspaceService>();
                services.AddSingleton<IWorkspaceDialogService, WorkspaceDialogService>();

                services.AddSingleton<IEnvironmentStore, JsonEnvironmentStore>();
                services.AddSingleton<IEnvironmentService, EnvironmentService>();
                services.AddSingleton<IEnvironmentDialogService, EnvironmentDialogService>();
                services.AddSingleton<IEditEnvironmentDialogService, EditEnvironmentDialogService>();

                services.AddSingleton<IProviderStore, JsonProviderStore>();
                services.AddSingleton<IProviderService, ProviderService>();
                services.AddSingleton<IProviderDialogService, ProviderDialogService>();
                services.AddSingleton<IEditProviderDialogService, EditProviderDialogService>();

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
