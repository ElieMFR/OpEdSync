using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Represents a response to the anime endpoint.
/// </summary>
/// <param name="Anime">List of found anime.</param>
/// <param name="Links">Pagination links.</param>
public record AnimeResponse(
    [property: JsonPropertyName("anime")] Collection<Anime> Anime,
    [property: JsonPropertyName("links")] PageLinks? Links
);
