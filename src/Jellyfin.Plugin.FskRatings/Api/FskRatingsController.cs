using Jellyfin.Plugin.FskRatings.Tmdb;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FskRatings.Api;

/// <summary>
/// API endpoints for the FSK Rating Updater plugin.
/// </summary>
[ApiController]
[Route("FskRatings")]
public class FskRatingsController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FskRatingsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FskRatingsController"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public FskRatingsController(
        ILibraryManager libraryManager,
        IHttpClientFactory httpClientFactory,
        ILogger<FskRatingsController> logger)
    {
        _libraryManager = libraryManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
    /// Tests whether a TMDb API key is accepted by TMDb. The key is taken from the
    /// request body so the (possibly unsaved) value from the settings form can be
    /// checked. Admin-only.
    /// </summary>
    /// <param name="request">The key to test.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The test result.</returns>
    [HttpPost("TestApiKey")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TestApiKeyResultDto>> TestApiKey(
        [FromBody] TestApiKeyRequestDto request,
        CancellationToken cancellationToken)
    {
        var apiKey = request?.ApiKey?.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            return new TestApiKeyResultDto { Success = false, Message = "No API key entered." };
        }

        try
        {
            var client = new TmdbClient(_httpClientFactory, _logger, apiKey);
            var valid = await client.ValidateApiKeyAsync(cancellationToken).ConfigureAwait(false);
            return new TestApiKeyResultDto
            {
                Success = valid,
                Message = valid ? "API key is valid." : "TMDb rejected this API key."
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "TMDb API key test failed to reach TMDb.");
            return new TestApiKeyResultDto { Success = false, Message = "Could not reach TMDb. Check the server's internet connection." };
        }
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
/// Request body for the TMDb API key test.
/// </summary>
public class TestApiKeyRequestDto
{
    /// <summary>
    /// Gets or sets the API key to test.
    /// </summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// Result of a TMDb API key test.
/// </summary>
public class TestApiKeyResultDto
{
    /// <summary>
    /// Gets or sets a value indicating whether the key was accepted.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets a human-readable result message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
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
