using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Pagination links of a response.
/// </summary>
/// <param name="Next">Absolute URL of the next page, null on the last page.</param>
public record PageLinks(
    [property: JsonPropertyName("next")] string? Next
);
