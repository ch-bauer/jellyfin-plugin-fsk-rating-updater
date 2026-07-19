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
