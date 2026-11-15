using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// An anime theme entry API resource represents a version of an anime theme.
///
/// For example, the ED theme of the Bakemonogatari anime has three anime theme entries to represent three versions.
/// </summary>
/// <param name="Id">The primary key of the resource.</param>
/// <param name="Version">The version number of the theme.</param>
/// <param name="Nsfw">Whether not safe for work content is included.</param>
/// <param name="Spoiler">Whether content is included that may spoil the viewer.</param>
/// <param name="Videos">Videos for the entry.</param>
public record AnimeThemeEntry(
    int Id,
    int? Version,
    bool Nsfw,
    bool Spoiler,
    Collection<Video> Videos
);
