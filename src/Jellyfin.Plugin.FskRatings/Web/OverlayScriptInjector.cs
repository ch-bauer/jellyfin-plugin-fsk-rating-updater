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

    /// <summary>
    /// Transformation callback invoked by the File Transformation plugin for index.html.
    /// Must stay public, static and string-returning — it is resolved by name via reflection.
    /// </summary>
    /// <param name="payload">The payload containing the current file contents.</param>
    /// <returns>The transformed contents (unchanged when the tag is already present).</returns>
    public static string TransformIndexHtml(TransformationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var contents = payload.Contents ?? string.Empty;
        if (string.IsNullOrEmpty(contents))
        {
            return contents;
        }

        return AddScriptTag(contents) ?? contents;
    }
}
