using System;

namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// Result of a check of the AnimeThemes API.
/// </summary>
public sealed class AnimeThemesCheck
{
    /// <summary>
    /// Gets or sets when the check was made.
    /// </summary>
    public DateTime CheckedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether AnimeThemes answered.
    /// </summary>
    public bool Reachable { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code, 0 when there was no answer.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets how long the answer took.
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the error when there was no answer.
    /// </summary>
    public string Error { get; set; } = string.Empty;
}
