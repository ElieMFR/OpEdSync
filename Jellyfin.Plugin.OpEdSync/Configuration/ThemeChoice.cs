namespace Jellyfin.Plugin.OpEdSync.Configuration;

/// <summary>
/// Which theme of an anime is taken first when it has several openings or endings.
/// </summary>
public enum ThemeChoice
{
    /// <summary>
    /// The most recent theme: the highest number (OP12 before OP3), without penalising what does not matter when
    /// listening (the source of the video, credits shown on screen).
    /// </summary>
    Latest,

    /// <summary>
    /// The first theme of the best quality, as versions up to 1.5.4.0 did.
    /// </summary>
    First,
}
