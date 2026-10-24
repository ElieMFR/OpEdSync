using System;

namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// An item without theme.
/// </summary>
public sealed class MissingItem
{
    /// <summary>
    /// Gets or sets the item ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the production year.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets why it has no theme (see <see cref="Reports.MissingReason"/>).
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
