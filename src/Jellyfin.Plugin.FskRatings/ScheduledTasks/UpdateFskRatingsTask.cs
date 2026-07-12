using Jellyfin.Data.Enums;
using Jellyfin.Plugin.FskRatings.Configuration;
using Jellyfin.Plugin.FskRatings.Normalization;
using Jellyfin.Plugin.FskRatings.Tmdb;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FskRatings.ScheduledTasks;

/// <summary>
/// Scheduled task that standardizes age ratings to the German FSK format and fills
/// missing/foreign ratings from TMDb's German certifications.
/// </summary>
public class UpdateFskRatingsTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdateFskRatingsTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateFskRatingsTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public UpdateFskRatingsTask(
        ILibraryManager libraryManager,
        IHttpClientFactory httpClientFactory,
        ILogger<UpdateFskRatingsTask> logger)
    {
        _libraryManager = libraryManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Update FSK age ratings";

    /// <inheritdoc />
    public string Key => "UpdateFskRatings";

    /// <inheritdoc />
    public string Description => "Standardizes age ratings to FSK-n format and fetches missing German certifications from TMDb.";

    /// <inheritdoc />
    public string Category => "FSK Rating Updater";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        var itemTypes = new List<BaseItemKind>();
        if (config.UpdateMovies)
        {
            itemTypes.Add(BaseItemKind.Movie);
        }

        if (config.UpdateSeries)
        {
            itemTypes.Add(BaseItemKind.Series);
        }

        if (itemTypes.Count == 0)
        {
            _logger.LogInformation("FSK Rating Updater: both movies and series are disabled in the plugin settings, nothing to do.");
            return;
        }

        var tmdbClient = string.IsNullOrWhiteSpace(config.TmdbApiKey)
            ? null
            : new TmdbClient(_httpClientFactory, _logger, config.TmdbApiKey.Trim());

        if (tmdbClient is null)
        {
            _logger.LogInformation("FSK Rating Updater: no TMDb API key configured, running in normalization-only mode.");
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = itemTypes.ToArray(),
            Recursive = true
        });

        _logger.LogInformation(
            "FSK Rating Updater: processing {Count} items (dry-run: {DryRun}, fallback: {Fallback}).",
            items.Count,
            config.DryRun,
            config.FallbackMode);

        int changed = 0, alreadyCorrect = 0, unresolved = 0, cleared = 0;

        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i];
            var current = item.OfficialRating;

            var newRating = await DetermineRatingAsync(item, current, config, tmdbClient, cancellationToken)
                .ConfigureAwait(false);

            if (!newRating.Resolved)
            {
                unresolved++;
            }
            else if (string.Equals(newRating.Value, current, StringComparison.Ordinal))
            {
                alreadyCorrect++;
            }
            else
            {
                if (newRating.Value is null)
                {
                    cleared++;
                }

                changed++;
                if (config.DryRun)
                {
                    _logger.LogInformation(
                        "[DryRun] {Name}: '{Old}' -> '{New}'",
                        item.Name,
                        current ?? "(none)",
                        newRating.Value ?? "(cleared)");
                }
                else
                {
                    _logger.LogInformation(
                        "{Name}: '{Old}' -> '{New}'",
                        item.Name,
                        current ?? "(none)",
                        newRating.Value ?? "(cleared)");

                    item.OfficialRating = newRating.Value;
                    await _libraryManager.UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            progress.Report(100.0 * (i + 1) / items.Count);
        }

        _logger.LogInformation(
            "FSK Rating Updater finished{DryRunSuffix}: {Changed} changed ({Cleared} cleared), {Correct} already correct, {Unresolved} unresolved.",
            config.DryRun ? " (dry-run, nothing written)" : string.Empty,
            changed,
            cleared,
            alreadyCorrect,
            unresolved);
    }

    private async Task<RatingResult> DetermineRatingAsync(
        BaseItem item,
        string? current,
        PluginConfiguration config,
        TmdbClient? tmdbClient,
        CancellationToken cancellationToken)
    {
        // Step 1: local normalization of already-German values.
        var normalized = FskNormalizer.TryNormalize(current);
        if (normalized is not null && !config.OverwriteExistingFsk)
        {
            return new RatingResult(normalized);
        }

        // Step 2: TMDb lookup.
        if (tmdbClient is not null)
        {
            var fromTmdb = await LookupTmdbAsync(item, tmdbClient, cancellationToken).ConfigureAwait(false);
            if (fromTmdb is not null)
            {
                return new RatingResult(fromTmdb);
            }
        }

        // TMDb had nothing but the existing value was already valid FSK — keep it normalized.
        if (normalized is not null)
        {
            return new RatingResult(normalized);
        }

        // Step 3: fallback.
        switch (config.FallbackMode)
        {
            case FallbackMode.MapFromForeign:
                var mapped = FskNormalizer.TryMapForeign(current);
                return mapped is not null ? new RatingResult(mapped) : RatingResult.Unresolved;
            case FallbackMode.ClearRating:
                return string.IsNullOrEmpty(current) ? RatingResult.Unresolved : new RatingResult(null);
            case FallbackMode.KeepUnchanged:
            default:
                return RatingResult.Unresolved;
        }
    }

    private async Task<string?> LookupTmdbAsync(BaseItem item, TmdbClient tmdbClient, CancellationToken cancellationToken)
    {
        var isMovie = item is Movie;
        var tmdbId = item.GetProviderId(MetadataProvider.Tmdb);

        if (string.IsNullOrEmpty(tmdbId))
        {
            var imdbId = item.GetProviderId(MetadataProvider.Imdb);
            if (string.IsNullOrEmpty(imdbId))
            {
                return null;
            }

            var found = await tmdbClient.FindByImdbIdAsync(imdbId, cancellationToken).ConfigureAwait(false);
            if (found is null)
            {
                return null;
            }

            tmdbId = found.Value.TmdbId;
            isMovie = found.Value.IsMovie;
        }

        var certification = isMovie
            ? await tmdbClient.GetMovieGermanCertificationAsync(tmdbId, cancellationToken).ConfigureAwait(false)
            : await tmdbClient.GetSeriesGermanCertificationAsync(tmdbId, cancellationToken).ConfigureAwait(false);

        // TMDb returns German certifications as bare numbers ("12"); normalize handles
        // both that and already-prefixed values defensively.
        return FskNormalizer.TryNormalize(certification);
    }

    private readonly record struct RatingResult(string? Value, bool Resolved = true)
    {
        public static RatingResult Unresolved => new(null, false);
    }
}
