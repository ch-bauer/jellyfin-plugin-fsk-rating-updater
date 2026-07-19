using Jellyfin.Plugin.FskRatings.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.FskRatings;

/// <summary>
/// Registers the plugin's services with the server's dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Preferred: register the overlay injection with the File Transformation
        // plugin when installed (same mechanism as Intro Skipper).
        serviceCollection.AddHostedService<FileTransformationRegistration>();

        // Fallback: request-time injection of the FSK overlay script into the web
        // client's index.html; works even when the web directory on disk is
        // read-only. Stands down when File Transformation handles the injection.
        serviceCollection.AddSingleton<IStartupFilter, OverlayInjectionStartupFilter>();
    }
}
