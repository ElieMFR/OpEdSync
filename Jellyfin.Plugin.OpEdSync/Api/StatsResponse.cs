using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// Statistics of the selected libraries.
/// </summary>
[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Plain response object.")]
public sealed class StatsResponse
{
    /// <summary>
    /// Gets or sets all libraries of the server, with their selection state.
    /// </summary>
    public IReadOnlyList<LibraryInfo> Libraries { get; set; } = [];

    /// <summary>
    /// Gets or sets when these figures were computed.
    /// </summary>
    public DateTime ComputedUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of series and movies in the selected libraries.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets how many of them have a theme (song or video), whatever its origin.
    /// </summary>
    public int WithTheme { get; set; }

    /// <summary>
    /// Gets or sets how many of them have a theme song written by the plugin.
    /// </summary>
    public int FromPlugin { get; set; }

    /// <summary>
    /// Gets or sets the number of items without theme, by reason.
    /// </summary>
    public Dictionary<string, int> Reasons { get; set; } = [];

    /// <summary>
    /// Gets or sets the items without theme (capped).
    /// </summary>
    public IReadOnlyList<MissingItem> Missing { get; set; } = [];

    /// <summary>
    /// Gets or sets the summary of the last run, null when the task never ran with this version.
    /// </summary>
    public LastRunInfo? LastRun { get; set; }
}
