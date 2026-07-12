using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FskRatings.Tmdb;

/// <summary>
/// Minimal TMDb client for fetching German (DE) certifications.
/// </summary>
public class TmdbClient
{
    private const string BaseUrl = "https://api.themoviedb.org/3";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly string _apiKey;
    private readonly Dictionary<string, string?> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="apiKey">The TMDb API key.</param>
    public TmdbClient(IHttpClientFactory httpClientFactory, ILogger logger, string apiKey)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = apiKey;
    }

    /// <summary>
    /// Gets the German certification for a movie, e.g. "12", or null when unavailable.
    /// </summary>
    /// <param name="tmdbId">The TMDb movie id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The certification string or null.</returns>
    public async Task<string?> GetMovieGermanCertificationAsync(string tmdbId, CancellationToken cancellationToken)
    {
        var cacheKey = "movie:" + tmdbId;
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var response = await GetAsync<ReleaseDatesResponse>(
            $"{BaseUrl}/movie/{tmdbId}/release_dates?api_key={_apiKey}",
            cancellationToken).ConfigureAwait(false);

        var germanEntry = response?.Results?.FirstOrDefault(r =>
            string.Equals(r.CountryCode, "DE", StringComparison.OrdinalIgnoreCase));

        // Prefer the theatrical release certification (type 3), fall back to any non-empty one.
        var certification = germanEntry?.ReleaseDates?
            .Where(d => !string.IsNullOrWhiteSpace(d.Certification))
            .OrderBy(d => d.Type == 3 ? 0 : 1)
            .Select(d => d.Certification!.Trim())
            .FirstOrDefault();

        _cache[cacheKey] = certification;
        return certification;
    }

    /// <summary>
    /// Gets the German content rating for a TV series, e.g. "12", or null when unavailable.
    /// </summary>
    /// <param name="tmdbId">The TMDb series id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rating string or null.</returns>
    public async Task<string?> GetSeriesGermanCertificationAsync(string tmdbId, CancellationToken cancellationToken)
    {
        var cacheKey = "tv:" + tmdbId;
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var response = await GetAsync<ContentRatingsResponse>(
            $"{BaseUrl}/tv/{tmdbId}/content_ratings?api_key={_apiKey}",
            cancellationToken).ConfigureAwait(false);

        var rating = response?.Results?
            .FirstOrDefault(r => string.Equals(r.CountryCode, "DE", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(r.Rating))?
            .Rating?.Trim();

        _cache[cacheKey] = rating;
        return rating;
    }

    /// <summary>
    /// Resolves a TMDb id from an IMDb id ("tt0111161"). Returns (tmdbId, isMovie) or null.
    /// </summary>
    /// <param name="imdbId">The IMDb id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The TMDb id and media type, or null.</returns>
    public async Task<(string TmdbId, bool IsMovie)?> FindByImdbIdAsync(string imdbId, CancellationToken cancellationToken)
    {
        var response = await GetAsync<FindResponse>(
            $"{BaseUrl}/find/{imdbId}?external_source=imdb_id&api_key={_apiKey}",
            cancellationToken).ConfigureAwait(false);

        var movie = response?.MovieResults?.FirstOrDefault();
        if (movie is not null)
        {
            return (movie.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), true);
        }

        var tv = response?.TvResults?.FirstOrDefault();
        if (tv is not null)
        {
            return (tv.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), false);
        }

        return null;
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        var client = _httpClientFactory.CreateClient(NamedClient.Default);

        try
        {
            using var response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                // Rate limited — wait briefly and retry once.
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                using var retry = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
                retry.EnsureSuccessStatusCode();
                return await retry.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "TMDb request failed: {Url}", url.Replace(_apiKey, "***", StringComparison.Ordinal));
            return null;
        }
    }

    private sealed class ReleaseDatesResponse
    {
        [JsonPropertyName("results")]
        public List<ReleaseDatesResult>? Results { get; set; }
    }

    private sealed class ReleaseDatesResult
    {
        [JsonPropertyName("iso_3166_1")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("release_dates")]
        public List<ReleaseDateEntry>? ReleaseDates { get; set; }
    }

    private sealed class ReleaseDateEntry
    {
        [JsonPropertyName("certification")]
        public string? Certification { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }
    }

    private sealed class ContentRatingsResponse
    {
        [JsonPropertyName("results")]
        public List<ContentRatingResult>? Results { get; set; }
    }

    private sealed class ContentRatingResult
    {
        [JsonPropertyName("iso_3166_1")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("rating")]
        public string? Rating { get; set; }
    }

    private sealed class FindResponse
    {
        [JsonPropertyName("movie_results")]
        public List<FindResult>? MovieResults { get; set; }

        [JsonPropertyName("tv_results")]
        public List<FindResult>? TvResults { get; set; }
    }

    private sealed class FindResult
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }
}
