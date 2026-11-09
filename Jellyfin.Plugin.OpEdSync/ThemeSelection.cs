using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.OpEdSync.Configuration;
using Jellyfin.Plugin.OpEdSync.Models;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// Orders the themes of an anime from the one to download first to the one to download last.
/// </summary>
internal static partial class ThemeSelection
{
    /// <summary>
    /// Lists the usable themes of an anime, best first.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="type">What is downloaded: the music or the video.</param>
    /// <param name="settings">The rules of this type.</param>
    /// <returns>One row per video of the anime that passes the rules. The first row of a theme is its best video.</returns>
    public static IReadOnlyList<FlattenedTheme> Order(Anime anime, MediaType type, MediaTypeConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(anime);
        ArgumentNullException.ThrowIfNull(settings);

        var candidates = anime.Themes
            .SelectMany(theme => theme.Entries.SelectMany(entry => entry.Videos.Select(video => new FlattenedTheme(theme, entry, video, video.Audio))))
            .Where(it => !settings.IgnoreOverlapping
                         || it.Video.Overlap == OverlapType.None
                         || (settings.AllowTransitions && it.Video.Overlap == OverlapType.Transition))
            .Where(it => !settings.IgnoreThemesWithCredits || it.Video.Creditless)
            .Where(it => it.Theme.Type != ThemeType.Other)
            .Where(it => !settings.IgnoreEDs || it.Theme.Type != ThemeType.ED)
            .Where(it => !settings.IgnoreOPs || it.Theme.Type != ThemeType.OP)

            // Some videos have no extracted audio track on AnimeThemes; they can't provide a theme song.
            .Where(it => type == MediaType.Video || it.Audio is not null);

        if (settings.Choice == ThemeChoice.First)
        {
            // The order the plugin has always used: quality, then the order of AnimeThemes (OP1, OP2... then ED1...).
            return candidates.OrderBy(Rate).ToList();
        }

        // Quality first for a video (credits and the source are on screen), only what is heard for a music theme.
        // The number only decides between themes of the same quality; the full quality only between the videos of
        // one theme, so that its best video is the first.
        return candidates
            .Select((theme, index) => (Theme: theme, Index: index))
            .OrderBy(it => IsVariant(it.Theme.Theme) ? 1 : 0)
            .ThenBy(it => type == MediaType.Audio ? ListeningPenalty(it.Theme) : Rate(it.Theme))
            .ThenBy(it => it.Theme.Theme.Type == ThemeType.OP ? 0 : 1)
            .ThenByDescending(it => Number(it.Theme.Theme) ?? int.MinValue)
            .ThenBy(it => Rate(it.Theme))
            .ThenBy(it => it.Index)
            .Select(it => it.Theme)
            .ToList();
    }

    /// <summary>
    /// Gets the number of a theme in the order of its anime: 12 for <c>OP12</c>, 3 for <c>ED3-EN</c>.
    /// The number is read from the slug, which AnimeThemes always fills: its <c>sequence</c> is empty for most anime.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The number, or <c>null</c> when neither the slug nor the sequence give one.</returns>
    public static int? Number(AnimeTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var match = SlugNumber().Match(theme.Slug ?? string.Empty);
        return match.Success && int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : theme.Sequence;
    }

    /// <summary>
    /// Scores a theme on everything the plugin cares about; the lowest is the best.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The score.</returns>
    public static double Rate(FlattenedTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        double score = 0;

        if (theme.Entry.Nsfw)
        {
            score += 10;
        }

        if (theme.Entry.Spoiler)
        {
            // Huge penalty for spoilers
            score += 50;
        }

        // Big penalties for overlap
        switch (theme.Video.Overlap)
        {
            case OverlapType.Over:
                score += 20;
                break;
            case OverlapType.Transition:
                score += 15;
                break;
        }

        // Small penalties for source
        switch (theme.Video.Source)
        {
            case VideoSource.LD:
            case VideoSource.VHS:
                score += 10;
                break;
            case VideoSource.WEB:
            case VideoSource.RAW:
                score += 5;
                break;
        }

        // Medium penalty for credits
        if (!theme.Video.Creditless)
        {
            score += 10;
        }

        return score;
    }

    /// <summary>
    /// Scores a theme on what matters for the music: a spoiler, a part of the episode over the music.
    /// The source of the video, the credits shown on screen and the adult flag (it describes the picture) change nothing to the music.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The score; the lowest is the best.</returns>
    public static double ListeningPenalty(FlattenedTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        double score = 0;
        if (theme.Entry.Spoiler)
        {
            score += 50;
        }

        score += theme.Video.Overlap switch
        {
            OverlapType.Over => 20,
            OverlapType.Transition => 15,
            _ => 0,
        };

        return score;
    }

    // A version in another language or for another broadcast ("English Version"...) has its own numbering and episodes.
    private static bool IsVariant(AnimeTheme theme) => !string.IsNullOrWhiteSpace(theme.Group);

    [GeneratedRegex(@"^(?:OP|ED)(?<number>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SlugNumber();
}
