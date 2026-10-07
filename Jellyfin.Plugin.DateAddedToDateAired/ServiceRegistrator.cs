using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.DateAddedToDateAired;

/// <summary>Registers the background event listener during plugin startup.</summary>
public sealed class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ReleaseDateProcessor>();
        serviceCollection.AddSingleton<IHostedService, ReleaseDateUpdateService>();
    }
}
