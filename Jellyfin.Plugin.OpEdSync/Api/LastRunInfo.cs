using System;

namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// Summary of the last run.
/// </summary>
public sealed class LastRunInfo
{
    /// <summary>
    /// Gets or sets the start of the run.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the run.
    /// </summary>
    public DateTime EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the run was cancelled.
    /// </summary>
    public bool Cancelled { get; set; }

    /// <summary>
    /// Gets or sets how many items were candidates.
    /// </summary>
    public int Candidates { get; set; }

    /// <summary>
    /// Gets or sets how many items were matched.
    /// </summary>
    public int Matched { get; set; }

    /// <summary>
    /// Gets or sets the number of theme files written.
    /// </summary>
    public int Added { get; set; }

    /// <summary>
    /// Gets or sets the number of theme files removed.
    /// </summary>
    public int Removed { get; set; }

    /// <summary>
    /// Gets or sets the number of failed downloads.
    /// </summary>
    public int Failed { get; set; }
}
