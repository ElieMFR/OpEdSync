namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Enum describing the theme types.
/// </summary>
public enum ThemeType
{
    /// <summary>
    /// Opening theme.
    /// </summary>
    OP,

    /// <summary>
    /// Ending theme.
    /// </summary>
    ED,

    /// <summary>
    /// A type the plugin does not know (an insert song, for instance); never used as a theme.
    /// </summary>
    Other
}
