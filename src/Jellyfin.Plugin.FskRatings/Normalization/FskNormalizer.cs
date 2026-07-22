using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.FskRatings.Normalization;

/// <summary>
/// Normalizes age-rating strings to the canonical German FSK format ("FSK-12").
/// The hyphenated form is the one Jellyfin's built-in German rating table (de.csv)
/// recognizes for parental controls.
/// </summary>
public static partial class FskNormalizer
{
    private static readonly int[] ValidFskLevels = [0, 6, 12, 16, 18];

    [GeneratedRegex(@"^\s*(?:FSK|DE)\s*[-:/ ]?\s*(\d{1,2})\s*\+?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex FskStyleRegex();

    [GeneratedRegex(@"^\s*ab\s+(\d{1,2})(?:\s+Jahren?)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex AbJahrenRegex();

    // Accepts a trailing "+" ("12+", "16+"), the age-gate form used by Amazon and
    // various streaming scrapers. The ValidFskLevels check below still rejects
    // numbers that are not real FSK levels (e.g. "7+", "13+").
    [GeneratedRegex(@"^\s*(\d{1,2})\s*\+?\s*$")]
    private static partial Regex BareNumberRegex();

    /// <summary>
    /// Tries to interpret <paramref name="rating"/> as a German (FSK-style) rating and
    /// returns the canonical form, e.g. "FSK 12" / "fsk12" / "DE-12" / "12" / "12+" → "FSK-12".
    /// Returns null when the value is not recognizably German.
    /// </summary>
    /// <param name="rating">The raw rating string.</param>
    /// <returns>The canonical "FSK-n" string, or null.</returns>
    public static string? TryNormalize(string? rating)
    {
        if (string.IsNullOrWhiteSpace(rating))
        {
            return null;
        }

        var trimmed = rating.Trim();

        // Textual FSK-0 / FSK-18 wordings used by some scrapers.
        if (trimmed.Equals("o.A.", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("oA", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("ohne Altersbeschränkung", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Ohne Altersbeschraenkung", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("FSK o.A.", StringComparison.OrdinalIgnoreCase))
        {
            return "FSK-0";
        }

        if (trimmed.Equals("Keine Jugendfreigabe", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("FSK Keine Jugendfreigabe", StringComparison.OrdinalIgnoreCase))
        {
            return "FSK-18";
        }

        var match = FskStyleRegex().Match(trimmed);
        if (!match.Success)
        {
            match = AbJahrenRegex().Match(trimmed);
        }

        if (!match.Success)
        {
            match = BareNumberRegex().Match(trimmed);
        }

        if (match.Success && int.TryParse(match.Groups[1].Value, out var level)
            && Array.IndexOf(ValidFskLevels, level) >= 0)
        {
            return "FSK-" + level;
        }

        return null;
    }

    /// <summary>
    /// Maps a foreign rating (MPAA, US TV, BBFC) to an approximate FSK equivalent.
    /// These are heuristics, not official FSK decisions. Returns null for unknown values.
    /// </summary>
    /// <param name="rating">The raw rating string.</param>
    /// <returns>The approximate "FSK-n" string, or null.</returns>
    public static string? TryMapForeign(string? rating)
    {
        if (string.IsNullOrWhiteSpace(rating))
        {
            return null;
        }

        return rating.Trim().ToUpperInvariant() switch
        {
            // MPAA (US movies)
            "G" => "FSK-0",
            "PG" => "FSK-6",
            "PG-13" => "FSK-12",
            "R" => "FSK-16",
            "NC-17" => "FSK-18",

            // US TV
            "TV-Y" or "TV-Y7" or "TV-G" => "FSK-0",
            "TV-PG" => "FSK-6",
            "TV-14" => "FSK-12",
            "TV-MA" => "FSK-16",

            // BBFC (UK) — bare "PG" is covered by MPAA above, same target anyway.
            "U" or "UC" => "FSK-0",
            "12A" => "FSK-12",
            "15" => "FSK-16",
            // Bare "12" and "18" are handled by TryNormalize as German values first;
            // if this method is reached they were not, so map them here too.
            "12" => "FSK-12",
            "18" => "FSK-18",
            "R18" => "FSK-18",

            _ => null
        };
    }
}
