using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// Pure rules about the theme files the plugin writes, kept apart from file system access so they can be tested.
/// </summary>
public static partial class ThemeFileRules
{
    /// <summary>
    /// Highest accepted degree of parallelism. AnimeThemes is a community service: more would only hammer it.
    /// </summary>
    public const int MaxParallelism = 8;

    /// <summary>
    /// Keeps the degree of parallelism in a range accepted by <see cref="System.Threading.Tasks.ParallelOptions"/>,
    /// which throws on 0 and treats negative values as unlimited.
    /// </summary>
    /// <param name="value">Configured value.</param>
    /// <returns>A value between 1 and <see cref="MaxParallelism"/>.</returns>
    public static int ClampParallelism(int value) => Math.Clamp(value, 1, MaxParallelism);

    /// <summary>
    /// Converts a volume between 0 and 1 to the percentage used in file names.
    /// </summary>
    /// <param name="volume">Volume between 0 and 1.</param>
    /// <returns>The rounded percentage.</returns>
    public static int ToPercent(double volume) => (int)Math.Round(Math.Clamp(volume, 0, 1) * 100, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Builds the name of a theme file written by the plugin.
    /// </summary>
    /// <param name="baseName">AnimeThemes file name, without extension.</param>
    /// <param name="volume">Volume between 0 and 1.</param>
    /// <param name="extension">Extension without dot.</param>
    /// <returns>The file name.</returns>
    public static string BuildFileName(string baseName, double volume, string extension)
        => string.Create(CultureInfo.InvariantCulture, $"{baseName}__{ToPercent(volume)}.{extension}");

    /// <summary>
    /// Tells whether a file name was produced by the plugin (<c>name__50.mp3</c>, <c>name__0.webm</c>).
    /// Anything else, such as a user supplied <c>theme.mp3</c>, belongs to someone else.
    /// </summary>
    /// <param name="fileName">File name, with or without directory.</param>
    /// <returns>Whether the plugin owns this file.</returns>
    public static bool IsPluginFile(string fileName) => PluginFilePattern().IsMatch(Path.GetFileName(fileName));

    /// <summary>
    /// Selects the plugin files that are not in the list of files to keep. Files the plugin did not write are never selected.
    /// </summary>
    /// <param name="existing">File names currently on disk.</param>
    /// <param name="keep">File names that must stay.</param>
    /// <returns>The file names that can be removed.</returns>
    public static IEnumerable<string> FilesToRemove(IEnumerable<string> existing, IEnumerable<string> keep)
    {
        var keepSet = keep.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return existing.Where(it => IsPluginFile(it) && !keepSet.Contains(Path.GetFileName(it)));
    }

    /// <summary>
    /// Selects the other volume variants of a file that was just written (same theme, different volume).
    /// </summary>
    /// <param name="existing">File names currently on disk.</param>
    /// <param name="target">File name that was just written.</param>
    /// <returns>The superseded file names.</returns>
    public static IEnumerable<string> SupersededVariants(IEnumerable<string> existing, string target)
    {
        var targetMatch = PluginFilePattern().Match(Path.GetFileName(target));
        if (!targetMatch.Success)
        {
            return [];
        }

        return existing.Where(it =>
        {
            var match = PluginFilePattern().Match(Path.GetFileName(it));
            return match.Success
                   && string.Equals(match.Groups["base"].Value, targetMatch.Groups["base"].Value, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(match.Groups["ext"].Value, targetMatch.Groups["ext"].Value, StringComparison.OrdinalIgnoreCase)
                   && match.Groups["volume"].Value != targetMatch.Groups["volume"].Value;
        });
    }

    /// <summary>
    /// Tells whether some plugin files were written with another volume than the configured one.
    /// </summary>
    /// <param name="existing">File names currently on disk.</param>
    /// <param name="volume">Configured volume between 0 and 1.</param>
    /// <returns>Whether at least one plugin file has an outdated volume.</returns>
    public static bool HasOutdatedVolume(IEnumerable<string> existing, double volume)
    {
        var expected = ToPercent(volume).ToString(CultureInfo.InvariantCulture);
        return existing
            .Select(it => PluginFilePattern().Match(Path.GetFileName(it)))
            .Any(it => it.Success && it.Groups["volume"].Value != expected);
    }

    [GeneratedRegex(@"^(?<base>.+)__(?<volume>\d{1,3})\.(?<ext>mp3|webm)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PluginFilePattern();
}
