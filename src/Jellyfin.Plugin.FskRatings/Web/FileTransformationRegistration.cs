using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.FskRatings.Web;

/// <summary>
/// Registers the overlay script injection with the File Transformation plugin when it is
/// installed (same mechanism as Intro Skipper). File Transformation then applies
/// <see cref="OverlayScriptInjector.TransformIndexHtml"/> whenever index.html is served.
/// When the plugin is not installed (or registration fails), the request-time middleware
/// (<see cref="OverlayInjectionStartupFilter"/>) takes over as fallback.
/// </summary>
public class FileTransformationRegistration : IHostedService
{
    /// <summary>
    /// Stable id identifying this transformation with the File Transformation plugin.
    /// </summary>
    private const string TransformationId = "9c1f9d0e-5a7b-4c62-8f3a-1d2e4b6c8a17";

    private readonly ILogger<FileTransformationRegistration> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransformationRegistration"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public FileTransformationRegistration(ILogger<FileTransformationRegistration> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether the transformation was registered with the
    /// File Transformation plugin. When true, the middleware fallback stands down.
    /// </summary>
    public static bool Registered { get; private set; }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var fileTransformationAssembly = AssemblyLoadContext.All
                .SelectMany(x => x.Assemblies)
                .FirstOrDefault(x => x.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) ?? false);

            if (fileTransformationAssembly is null)
            {
                _logger.LogInformation("File Transformation plugin not found; FSK overlay uses request-time middleware injection.");
                return Task.CompletedTask;
            }

            var pluginInterfaceType = fileTransformationAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            var registerMethod = pluginInterfaceType?.GetMethod("RegisterTransformation");
            if (registerMethod is null)
            {
                _logger.LogWarning("File Transformation plugin found but its registration interface is missing; FSK overlay falls back to middleware injection.");
                return Task.CompletedTask;
            }

            var payload = new JObject
            {
                { "id", TransformationId },
                { "fileNamePattern", "index.html" },
                { "callbackAssembly", GetType().Assembly.FullName },
                { "callbackClass", typeof(OverlayScriptInjector).FullName },
                { "callbackMethod", nameof(OverlayScriptInjector.TransformIndexHtml) }
            };

            registerMethod.Invoke(null, [payload]);
            Registered = true;
            _logger.LogInformation("FSK overlay script registered with the File Transformation plugin.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not register the FSK overlay with the File Transformation plugin; falling back to middleware injection.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
