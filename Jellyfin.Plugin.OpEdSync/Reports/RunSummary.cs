using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.OpEdSync.Reports;

/// <summary>
/// Result of the last run, saved next to the plugin configuration.
/// </summary>
public sealed class RunSummary
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
    /// Gets or sets a value indicating whether the run was cancelled before its end.
    /// </summary>
    public bool Cancelled { get; set; }

    /// <summary>
    /// Gets or sets how many items were candidates.
    /// </summary>
    public int Candidates { get; set; }

    /// <summary>
    /// Gets or sets how many items were matched with an anime.
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

    /// <summary>
    /// Gets or sets why each item without theme stayed without one.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Deserialized from JSON.")]
    public Dictionary<Guid, MissingReason> Reasons { get; set; } = [];
}
