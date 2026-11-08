using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Represents an <a href="https://api-docs.animethemes.moe/animetheme/">anime theme</a>.
/// </summary>
/// <param name="Id">The primary key of the resource.</param>
/// <param name="Type">The type of the sequence [OP, ED].</param>
/// <param name="Sequence">The numeric ordering of the theme.</param>
/// <param name="Group">Used to distinguish sequence belongs to dubs, rebroadcasts, etc.</param>
/// <param name="Slug">The URL slug &amp; route key of the resource.</param>
/// <param name="Entries">Entries of the theme.</param>
public record AnimeTheme(
    int Id,
    ThemeType? Type,
    int? Sequence,
    string? Group,
    string? Slug,
    Collection<AnimeThemeEntry> Entries
);
