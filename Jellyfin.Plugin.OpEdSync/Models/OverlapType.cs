namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Types of overlap between episode and theme.
/// </summary>
public enum OverlapType
{
    /// <summary>
    /// No overlap.
    /// </summary>
    None,

    /// <summary>
    /// Some overlap.
    /// </summary>
    Transition,

    /// <summary>
    /// Complete overlap.
    /// </summary>
    Over
}
