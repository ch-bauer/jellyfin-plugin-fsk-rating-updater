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
    /// Gets or sets the ids of the libraries the task is restricted to. Each entry is
    /// the item id (GUID) of a library as returned by the virtual folders API. When
    /// empty, every library is processed (backward-compatible default).
    /// </summary>
    public string[] LibraryIds { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets a value indicating whether movies are processed.
    /// </summary>
    public bool UpdateMovies { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether TV series are processed.
    /// </summary>
    public bool UpdateSeries { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether seasons and episodes are processed
    /// in addition to the series itself. Only effective when <see cref="UpdateSeries"/> is on.
    /// </summary>
    public bool UpdateSeasonsAndEpisodes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether items that already carry a valid FSK
    /// rating are re-fetched from TMDb instead of only being reformatted.
    /// </summary>
    public bool OverwriteExistingFsk { get; set; } = false;

    /// <summary>
    /// Gets or sets the behavior for items without a determinable German rating.
    /// </summary>
    public FallbackMode FallbackMode { get; set; } = FallbackMode.KeepUnchanged;

    /// <summary>
    /// Gets or sets a value indicating whether a Netflix-style FSK badge is shown
    /// in the web client for a few seconds when playback starts.
    /// </summary>
    public bool EnablePlaybackOverlay { get; set; } = true;

    /// <summary>
    /// Gets or sets how long the playback overlay stays visible, in seconds (1-30).
    /// </summary>
    public int OverlayDurationSeconds { get; set; } = 5;
}
