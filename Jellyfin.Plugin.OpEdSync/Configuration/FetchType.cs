namespace Jellyfin.Plugin.OpEdSync.Configuration;

/// <summary>
/// Enum describing how files should be fetched.
/// </summary>
public enum FetchType
{
    /// <summary>
    /// Download nothing.
    /// </summary>
    None,

    /// <summary>
    /// Download only one file.
    /// </summary>
    Single,

    /// <summary>
    /// Download all files.
    /// </summary>
    All
}
