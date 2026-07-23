using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

            // Build the payload as a plain JSON string so we never touch Newtonsoft.Json ourselves.
            // The File Transformation plugin bundles its own copy of Newtonsoft.Json; a JObject built
            // from *our* bundled copy is a distinct runtime type from *its* JObject (same name, different
            // assembly identity), so passing one through reflection fails CheckValue with the nonsensical
            // "JObject cannot be converted to JObject". Instead we materialize the argument from the
            // register method's own parameter type, guaranteeing an exact type match.
            var payloadJson = JsonSerializer.Serialize(new
            {
                id = TransformationId,
                fileNamePattern = "index.html",
                callbackAssembly = GetType().Assembly.FullName,
                callbackClass = typeof(OverlayScriptInjector).FullName,
                callbackMethod = nameof(OverlayScriptInjector.TransformIndexHtml)
            });

            var parameterType = registerMethod.GetParameters().FirstOrDefault()?.ParameterType;
            if (parameterType is null)
            {
                _logger.LogWarning("File Transformation plugin's registration method has an unexpected signature; FSK overlay falls back to middleware injection.");
                return Task.CompletedTask;
            }

            object payloadArg;
            if (parameterType == typeof(string))
            {
                payloadArg = payloadJson;
            }
            else
            {
                // parameterType is the File Transformation plugin's own JObject. Parse the JSON with
                // that exact type's static Parse(string) so the argument's identity matches the method.
                var parseMethod = parameterType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
                if (parseMethod is null)
                {
                    _logger.LogWarning("File Transformation plugin's payload type '{Type}' has no Parse(string) method; FSK overlay falls back to middleware injection.", parameterType.FullName);
                    return Task.CompletedTask;
                }

                payloadArg = parseMethod.Invoke(null, [payloadJson])!;
            }

            registerMethod.Invoke(null, [payloadArg]);
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
