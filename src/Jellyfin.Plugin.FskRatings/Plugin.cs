using Jellyfin.Plugin.FskRatings.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FskRatings;

/// <summary>
/// The FSK Ratings plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{Plugin}"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        InjectOverlayScript(applicationPaths, logger);
    }

    /// <inheritdoc />
    public override string Name => "FSK Rating Updater";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("b7c3f1e2-9a4d-4e8b-b0c6-2f5d8a913c47");

    /// <inheritdoc />
    public override string Description =>
        "Standardizes age ratings to the German FSK format (FSK-0/6/12/16/18) and fills in missing or foreign ratings using official German certifications from TMDb.";

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = "fskOverlay.js",
                EmbeddedResourcePath = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}.Web.fskOverlay.js",
                    GetType().Namespace)
            }
        };
    }

    private static void InjectOverlayScript(IApplicationPaths applicationPaths, ILogger<Plugin> logger)
    {
        var webPath = applicationPaths.WebPath;
        if (string.IsNullOrEmpty(webPath))
        {
            return;
        }

        var indexPath = Path.Join(webPath, "index.html");
        try
        {
            if (!File.Exists(indexPath))
            {
                logger.LogWarning("Web client index.html not found at {Path}; the FSK playback overlay will not be available.", indexPath);
                return;
            }

            var contents = File.ReadAllText(indexPath);
            var updated = Web.OverlayScriptInjector.AddScriptTag(contents);
            if (updated is null)
            {
                return;
            }

            File.WriteAllText(indexPath, updated);
            logger.LogInformation("FSK playback overlay script injected into {Path}.", indexPath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            logger.LogWarning(
                ex,
                "Could not inject the FSK playback overlay script into {Path} (web directory may be read-only). To use the overlay, add this tag manually before </head>: {Tag}",
                indexPath,
                Web.OverlayScriptInjector.ScriptTag);
        }
    }
}
