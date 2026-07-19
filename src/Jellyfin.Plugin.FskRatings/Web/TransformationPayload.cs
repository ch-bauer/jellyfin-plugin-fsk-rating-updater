using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.FskRatings.Web;

/// <summary>
/// Payload passed by the File Transformation plugin to a registered transformation callback.
/// </summary>
public class TransformationPayload
{
    /// <summary>
    /// Gets or sets the current contents of the file being transformed.
    /// </summary>
    [JsonPropertyName("contents")]
    public string? Contents { get; set; }
}
