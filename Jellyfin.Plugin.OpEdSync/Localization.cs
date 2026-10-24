using System;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// French when Jellyfin is in French, English otherwise.
/// </summary>
public static class Localization
{
    /// <summary>
    /// Tells whether a culture name is French ("fr", "fr-FR", "fr-CA"...).
    /// </summary>
    /// <param name="culture">Culture name, may be empty.</param>
    /// <returns>Whether French texts should be used.</returns>
    public static bool IsFrench(string? culture)
        => culture is not null && (culture.Equals("fr", StringComparison.OrdinalIgnoreCase) || culture.StartsWith("fr-", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Picks the text matching a culture.
    /// </summary>
    /// <param name="culture">Culture name.</param>
    /// <param name="french">French text.</param>
    /// <param name="english">English text.</param>
    /// <returns>The text to show.</returns>
    public static string Pick(string? culture, string french, string english) => IsFrench(culture) ? french : english;
}
