using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// Version of the plugin, state of what it depends on.
/// </summary>
[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Plain response object.")]
public sealed class StatusResponse
{
    /// <summary>
    /// Gets or sets the version of the running plugin.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugins OpEdSync relies on.
    /// </summary>
    public IReadOnlyList<DependencyInfo> Dependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the Android replay fix is injected.
    /// </summary>
    public bool AndroidFixActive { get; set; }

    /// <summary>
    /// Gets or sets the last check of AnimeThemes.
    /// </summary>
    public AnimeThemesCheck? AnimeThemes { get; set; }
}
