using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.FskRatings.Configuration;

/// <summary>
/// What to do with items for which no German rating could be determined.
/// </summary>
public enum FallbackMode
{
    /// <summary>
    /// Keep the existing rating untouched.
    /// </summary>
    KeepUnchanged = 0,

    /// <summary>
    /// Map foreign ratings (MPAA, BBFC, US TV) to an approximate FSK equivalent.
    /// </summary>
    MapFromForeign = 1,

    /// <summary>
    /// Clear the rating so the item shows as unrated.
    /// </summary>
    ClearRating = 2
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the TMDb API key. When empty, TMDb lookups are skipped and only
    /// local normalization is performed.
    /// </summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the task only logs proposed changes
    /// without writing anything.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether movies are processed.
    /// </summary>
    public bool UpdateMovies { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether TV series are processed.
    /// </summary>
    public bool UpdateSeries { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether items that already carry a valid FSK
    /// rating are re-fetched from TMDb instead of only being reformatted.
    /// </summary>
    public bool OverwriteExistingFsk { get; set; } = false;

    /// <summary>
    /// Gets or sets the behavior for items without a determinable German rating.
    /// </summary>
    public FallbackMode FallbackMode { get; set; } = FallbackMode.KeepUnchanged;
}
