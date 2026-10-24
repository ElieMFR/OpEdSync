namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Outcome of a theme download.
/// </summary>
public enum DownloadResult
{
    /// <summary>
    /// The file was already on disk.
    /// </summary>
    AlreadyThere,

    /// <summary>
    /// The file was downloaded and converted.
    /// </summary>
    Downloaded,

    /// <summary>
    /// The download or the conversion failed.
    /// </summary>
    Failed,
}
