namespace Jellyfin.Plugin.OpEdSync.Configuration;

/// <summary>
/// Determines which entry of a series is used to look up themes.
/// </summary>
public enum ThemeSource
{
    /// <summary>
    /// Use the IDs of the series itself (usually the first season).
    /// </summary>
    Series = 0,

    /// <summary>
    /// Use the IDs of the most recent season that is known to AnimeThemes, falling back to the series.
    /// </summary>
    LatestSeason = 1,
}
