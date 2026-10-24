namespace Jellyfin.Plugin.OpEdSync.Reports;

/// <summary>
/// Why an item ended a run without a theme. Higher values are more informative and win when several apply.
/// </summary>
public enum MissingReason
{
    /// <summary>
    /// The item was not looked at by the last run (added since, or never run).
    /// </summary>
    NotAnalysed = 0,

    /// <summary>
    /// Neither the item nor its seasons have an AniDB, MyAnimeList or AniList ID.
    /// </summary>
    NoId = 1,

    /// <summary>
    /// AnimeThemes does not know any of the item's IDs.
    /// </summary>
    NotOnAnimeThemes = 2,

    /// <summary>
    /// AnimeThemes knows the anime but has no video for it yet.
    /// </summary>
    NoVideo = 3,

    /// <summary>
    /// The anime found has a release year too far from the item's.
    /// </summary>
    YearMismatch = 4,

    /// <summary>
    /// Themes exist, but none passes the configured filters.
    /// </summary>
    Filtered = 5,

    /// <summary>
    /// The download or the conversion failed.
    /// </summary>
    DownloadFailed = 6,

    /// <summary>
    /// The lookup on AnimeThemes failed (network, server error).
    /// </summary>
    LookupFailed = 7,
}
