using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.FskRatings.Api;

/// <summary>
/// API endpoints for the FSK Rating Updater plugin.
/// </summary>
[ApiController]
[Route("FskRatings")]
public class FskRatingsController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="FskRatingsController"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    public FskRatingsController(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Gets the media libraries the task can be restricted to. Requires an admin
    /// session because it is only used by the plugin configuration page.
    /// </summary>
    /// <returns>The available libraries.</returns>
    [HttpGet("Libraries")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<LibraryDto>> GetLibraries()
    {
        return _libraryManager.GetVirtualFolders()
            .Select(folder => new LibraryDto
            {
                Id = folder.ItemId,
                Name = folder.Name,
                CollectionType = folder.CollectionType?.ToString()
            })
            .OrderBy(library => library.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Gets the playback overlay settings needed by the injected web client script.
    /// Anonymous on purpose: the script runs for every user and the response
    /// contains nothing sensitive.
    /// </summary>
    /// <returns>The overlay configuration.</returns>
    [HttpGet("OverlayConfig")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<OverlayConfigDto> GetOverlayConfig()
    {
        var config = Plugin.Instance?.Configuration;
        return new OverlayConfigDto
        {
            Enabled = config?.EnablePlaybackOverlay ?? false,
            DurationSeconds = Math.Clamp(config?.OverlayDurationSeconds ?? 5, 1, 30)
        };
    }
}

/// <summary>
/// A media library the task can be restricted to.
/// </summary>
public class LibraryDto
{
    /// <summary>
    /// Gets or sets the library item id (GUID).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the library display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection type (e.g. movies, tvshows), if any.
    /// </summary>
    public string? CollectionType { get; set; }
}

/// <summary>
/// Playback overlay settings exposed to the web client.
/// </summary>
public class OverlayConfigDto
{
    /// <summary>
    /// Gets or sets a value indicating whether the overlay is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the overlay display duration in seconds.
    /// </summary>
    public int DurationSeconds { get; set; }
}
