namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// A plugin OpEdSync relies on.
/// </summary>
public sealed class DependencyInfo
{
    /// <summary>
    /// Gets or sets the plugin name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what it is used for: "Ids" (gives the IDs used to find themes) or "AndroidFix".
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether it is installed.
    /// </summary>
    public bool Installed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is active.
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Gets or sets its version.
    /// </summary>
    public string Version { get; set; } = string.Empty;
}
