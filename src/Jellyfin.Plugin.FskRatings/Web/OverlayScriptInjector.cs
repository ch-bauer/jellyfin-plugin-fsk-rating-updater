namespace Jellyfin.Plugin.FskRatings.Web;

/// <summary>
/// Pure string logic for injecting the overlay script tag into the web client's index.html.
/// Kept free of Jellyfin dependencies so it is unit-testable.
/// </summary>
public static class OverlayScriptInjector
{
    /// <summary>
    /// The script tag injected into the web client's index.html to load the playback overlay.
    /// </summary>
    public const string ScriptTag = "<script src=\"configurationpage?name=fskOverlay.js\" defer></script>";

    /// <summary>
    /// Adds the overlay script tag to the given index.html contents, directly before
    /// the closing head tag.
    /// </summary>
    /// <param name="html">The current index.html contents.</param>
    /// <returns>The updated contents, or null when the tag is already present or no head tag was found.</returns>
    public static string? AddScriptTag(string html)
    {
        if (html.Contains(ScriptTag, StringComparison.Ordinal))
        {
            return null;
        }

        var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headEnd < 0)
        {
            return null;
        }

        return html.Insert(headEnd, ScriptTag);
    }
}
