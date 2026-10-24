using System;

namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// A library and whether the plugin works on it.
/// </summary>
public sealed class LibraryInfo
{
    /// <summary>
    /// Gets or sets the library ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the library name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the plugin works on this library.
    /// </summary>
    public bool Included { get; set; }

    /// <summary>
    /// Gets or sets the number of series and movies in the library.
    /// </summary>
    public int ItemCount { get; set; }
}
