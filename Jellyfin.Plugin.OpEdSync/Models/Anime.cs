using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Represents an anime. Subset of <a href="https://api-docs.animethemes.moe/anime">anime</a>.
/// </summary>
public record Anime(
    int Id,
    string Name,
    string Slug,
    int? Year,
    Collection<Resource> Resources,
    Collection<AnimeTheme> Themes
)
{
    /// <summary>
    /// Gets the AniDB ID of this anime if available in payload.
    /// </summary>
    public int? AniDbId => GetExternalId(Sites.ANIDB);

    /// <summary>
    /// Gets the external ID of this anime on the given site, if available in payload.
    /// </summary>
    /// <param name="site">Site name, see <see cref="Sites"/>.</param>
    /// <returns>The external ID or null.</returns>
    public int? GetExternalId(string site) => Resources.FirstOrDefault(it => it.Site == site)?.ExternalId;
}
