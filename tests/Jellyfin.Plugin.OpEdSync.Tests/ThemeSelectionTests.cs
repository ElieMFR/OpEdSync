using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Serialization;
using Jellyfin.Plugin.OpEdSync.Configuration;
using Jellyfin.Plugin.OpEdSync.Models;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// 2026-11-09: a series with twenty openings used to get OP1 (or the first one without credits, Bleach's OP9), never the
// latest: the newest openings only exist from a web source and without a version free of credits, which cost them
// points against an old DVD. The long series are real answers of AnimeThemes, recorded the same day.
public class ThemeSelectionTests
{
    private static Anime Real(string slug)
    {
        using var stream = typeof(ThemeSelectionTests).Assembly.GetManifestResourceStream("Jellyfin.Plugin.OpEdSync.Tests.Fixtures.animethemes-long-series.json")!;
        using var document = JsonDocument.Parse(new StreamReader(stream).ReadToEnd());
        return AnimeThemesGraphQl.Parse(document).Anime.Single(it => it.Slug == slug);
    }

    // The settings of the music part as a user would have them.
    private static MediaTypeConfiguration Settings(ThemeChoice choice) => new()
    {
        FetchType = FetchType.Single,
        IgnoreOverlapping = true,
        AllowTransitions = true,
        Choice = choice,
    };

    // What the downloader keeps with "one, the best": the first theme, once per theme.
    private static string Pick(Anime anime, MediaType type, ThemeChoice choice, Action<MediaTypeConfiguration>? adjust = null)
    {
        var settings = Settings(choice);
        adjust?.Invoke(settings);
        return ThemeSelection.Order(anime, type, settings).DistinctBy(it => it.Theme.Id).First().Theme.Slug!;
    }

    [Theory]
    [InlineData("bleach", "OP9")]
    [InlineData("cowboy_bebop", "ED1")]
    [InlineData("detective_conan", "OP1")]
    [InlineData("naruto_shippuuden", "OP1")]
    [InlineData("one_piece", "OP1")]
    [InlineData("dragon_ball", "OP1")]
    public void First_KeepsTheChoicesOfEarlierVersions(string slug, string expected)
    {
        Assert.Equal(expected, Pick(Real(slug), MediaType.Audio, ThemeChoice.First));
    }

    [Theory]
    [InlineData("bleach", "OP15")]
    [InlineData("detective_conan", "OP62")]
    [InlineData("naruto_shippuuden", "OP20")]
    [InlineData("one_piece", "OP29")]
    [InlineData("cowboy_bebop", "OP1")]
    [InlineData("dragon_ball", "OP1")]
    public void Latest_TakesTheHighestOpening_ForTheMusic(string slug, string expected)
    {
        Assert.Equal(expected, Pick(Real(slug), MediaType.Audio, ThemeChoice.Latest));
    }

    // A video shows its credits and its source: quality still comes first, the newest of the best.
    [Fact]
    public void Latest_KeepsQualityFirst_ForTheVideo()
    {
        Assert.Equal("OP38", Pick(Real("detective_conan"), MediaType.Video, ThemeChoice.Latest));
        Assert.Equal("OP62", Pick(Real("detective_conan"), MediaType.Audio, ThemeChoice.Latest));
    }

    [Fact]
    public void Latest_NeverTakesTheDubbedVersion_WhenTheOriginalExists()
    {
        var anime = Real("dragon_ball");

        var rows = ThemeSelection.Order(anime, MediaType.Audio, Settings(ThemeChoice.Latest));

        Assert.Contains(rows, it => it.Theme.Group == "English Version");
        Assert.All(rows.TakeWhile(it => it.Theme.Group is null), it => Assert.DoesNotContain("EN", it.Theme.Slug!, StringComparison.Ordinal));
        Assert.Null(rows[0].Theme.Group);
    }

    [Theory]
    [InlineData("OP12", 12)]
    [InlineData("ED3-EN", 3)]
    [InlineData("op7", 7)]
    [InlineData("ED10-ShounenHen", 10)]
    public void Number_IsReadFromTheSlug(string slug, int expected)
    {
        Assert.Equal(expected, ThemeSelection.Number(Theme(1, slug)));
    }

    // `sequence` is empty for most anime on AnimeThemes: the slug is what counts. It only stands in for a slug without number.
    [Fact]
    public void Number_FallsBackToTheSequence_ThenToNothing()
    {
        Assert.Equal(4, ThemeSelection.Number(Theme(1, "Special", sequence: 4)));
        Assert.Null(ThemeSelection.Number(Theme(1, "Special")));
        Assert.Equal(9, ThemeSelection.Number(Theme(1, "OP9", sequence: 2)));
    }

    [Fact]
    public void Latest_TakesTheHigherNumber_AmongThemesOfTheSameQuality()
    {
        var anime = Make(Theme(1, "OP1"), Theme(2, "OP3"), Theme(3, "OP2"));

        Assert.Equal("OP3", Pick(anime, MediaType.Audio, ThemeChoice.Latest));
        Assert.Equal(["OP3", "OP2", "OP1"], ThemeSelection.Order(anime, MediaType.Audio, Settings(ThemeChoice.Latest)).Select(it => it.Theme.Slug));
    }

    [Fact]
    public void Latest_PrefersAnOpening_ToAnEnding_EvenWithALowerNumber()
    {
        var anime = Make(Theme(1, "ED7", ThemeType.ED), Theme(2, "OP1"));

        Assert.Equal("OP1", Pick(anime, MediaType.Audio, ThemeChoice.Latest));
    }

    [Fact]
    public void Latest_PutsAVariantAfterTheOriginals_AndTakesItWhenAloneOnly()
    {
        // A dubbed version has its own numbering (Pokémon's OP3-EN is not later than OP3): it never wins by its number.
        var withOriginal = Make(Theme(1, "OP1"), Theme(2, "OP2"), Theme(3, "OP5-EN", group: "English Version"));
        var onlyVariant = Make(Theme(3, "OP2-EN", group: "English Version"));

        Assert.Equal("OP2", Pick(withOriginal, MediaType.Audio, ThemeChoice.Latest));
        Assert.Equal("OP2-EN", Pick(onlyVariant, MediaType.Audio, ThemeChoice.Latest));
        Assert.Equal("OP5-EN", ThemeSelection.Order(withOriginal, MediaType.Audio, Settings(ThemeChoice.Latest)).Last().Theme.Slug);
    }

    [Fact]
    public void Latest_SkipsTheNewestThemeWhenItHasASpoiler()
    {
        var anime = Make(Theme(1, "OP1"), Theme(2, "OP2"), Theme(3, "OP3", spoiler: true));

        Assert.Equal("OP2", Pick(anime, MediaType.Audio, ThemeChoice.Latest));
    }

    [Fact]
    public void Latest_PrefersACleanThemeToOneOverlappingTheEpisode()
    {
        var anime = Make(Theme(1, "OP1"), Theme(2, "OP2", overlap: OverlapType.Transition));

        Assert.Equal("OP1", Pick(anime, MediaType.Audio, ThemeChoice.Latest));
    }

    // The adult flag describes the picture, not the sound: it does not change the music, it does change a video.
    // Slime season 4: OP1 is clean, the newer OP2 is flagged.
    [Fact]
    public void Latest_IgnoresTheAdultFlag_ForTheMusicOnly()
    {
        var anime = Make(Theme(1, "OP1"), Theme(2, "OP2", nsfw: true));

        Assert.Equal("OP2", Pick(anime, MediaType.Audio, ThemeChoice.Latest));
        Assert.Equal("OP1", Pick(anime, MediaType.Video, ThemeChoice.Latest));
    }

    // The same holds for the order of earlier versions: only the picture is adult, so only the video is affected.
    [Fact]
    public void First_IgnoresTheAdultFlag_ForTheMusicOnly()
    {
        var anime = Make(Theme(1, "OP1", nsfw: true), Theme(2, "OP2"));

        // Music: the flag is ignored, the order of AnimeThemes decides. Video: the flagged OP1 comes last.
        Assert.Equal("OP1", Pick(anime, MediaType.Audio, ThemeChoice.First));
        Assert.Equal("OP2", Pick(anime, MediaType.Video, ThemeChoice.First));
    }

    // Credits on screen and the source of the video do not change the music; they do change a video.
    [Fact]
    public void Latest_IgnoresCreditsAndSource_ForTheMusicOnly()
    {
        var anime = Make(Theme(1, "OP2", creditless: true, source: VideoSource.BD), Theme(2, "OP3", creditless: false, source: VideoSource.WEB));

        Assert.Equal("OP3", Pick(anime, MediaType.Audio, ThemeChoice.Latest));
        Assert.Equal("OP2", Pick(anime, MediaType.Video, ThemeChoice.Latest));
    }

    [Fact]
    public void First_KeepsTheOrderOfQualityThenOfAnimeThemes()
    {
        var anime = Make(Theme(1, "OP1", creditless: false), Theme(2, "OP2", creditless: true), Theme(3, "OP3", creditless: true));

        Assert.Equal(["OP2", "OP3", "OP1"], ThemeSelection.Order(anime, MediaType.Audio, Settings(ThemeChoice.First)).Select(it => it.Theme.Slug));
    }

    [Fact]
    public void Latest_PutsTheBestVideoOfATheme_First()
    {
        var web = Video(10, VideoSource.WEB, creditless: false);
        var bluray = Video(11, VideoSource.BD, creditless: true);
        var anime = Make(Theme(1, "OP1", videos: [web, bluray]));

        var row = ThemeSelection.Order(anime, MediaType.Audio, Settings(ThemeChoice.Latest)).First();

        Assert.Equal(11, row.Video.Id);
    }

    [Fact]
    public void TheFiltersOfTheSettings_StillApply()
    {
        var anime = Make(
            Theme(1, "OP1", creditless: false),
            Theme(2, "ED1", ThemeType.ED),
            Theme(3, "IN1", ThemeType.Other),
            Theme(4, "OP2", overlap: OverlapType.Over),
            Theme(5, "OP3", hasAudio: false));

        string[] Slugs(Action<MediaTypeConfiguration> adjust, MediaType type = MediaType.Audio)
        {
            var settings = Settings(ThemeChoice.Latest);
            adjust(settings);
            return ThemeSelection.Order(anime, type, settings).Select(it => it.Theme.Slug!).Order().ToArray();
        }

        Assert.Equal(["ED1", "OP1"], Slugs(_ => { }));
        Assert.Equal(["OP1"], Slugs(s => s.IgnoreEDs = true));
        Assert.Equal(["ED1"], Slugs(s => s.IgnoreOPs = true));
        Assert.Equal(["ED1"], Slugs(s => s.IgnoreThemesWithCredits = true));
        Assert.Equal(["ED1", "OP1", "OP2"], Slugs(s => s.IgnoreOverlapping = false));
        Assert.Equal(["ED1", "OP1", "OP3"], Slugs(_ => { }, MediaType.Video));
    }

    [Fact]
    public void TheChoiceIsLatest_ByDefault_AndForSettingsSavedByEarlierVersions()
    {
        Assert.Equal(ThemeChoice.Latest, new MediaTypeConfiguration().Choice);

        const string older = "<?xml version=\"1.0\"?><PluginConfiguration><AudioSettings><FetchType>Single</FetchType><Volume>0.5</Volume></AudioSettings></PluginConfiguration>";
        const string saved = "<?xml version=\"1.0\"?><PluginConfiguration><AudioSettings><FetchType>Single</FetchType><Choice>First</Choice></AudioSettings></PluginConfiguration>";
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        Assert.Equal(ThemeChoice.Latest, ((PluginConfiguration)serializer.Deserialize(new StringReader(older))!).AudioSettings.Choice);
        Assert.Equal(ThemeChoice.First, ((PluginConfiguration)serializer.Deserialize(new StringReader(saved))!).AudioSettings.Choice);
    }

    [Fact]
    public void ConfigurationPage_OffersBothChoices_InBothLanguages()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.OpEdSync.Configuration.configPage.html")!;
        var html = new StreamReader(stream).ReadToEnd();

        Assert.Contains("id=\"opsThemeChoice\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"Latest\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"First\"", html, StringComparison.Ordinal);
        Assert.Contains("audio.Choice = video.Choice", html, StringComparison.Ordinal);
        foreach (var key in new[] { "themeChoice", "choiceLatest", "choiceFirst", "themeChoiceHelp" })
        {
            Assert.Equal(2, html.Split($"\"{key}\":").Length - 1);
        }
    }

    private static Anime Make(params AnimeTheme[] themes) => new(1, "A", "a", 2020, [], new Collection<AnimeTheme>(themes));

    private static Video Video(int id, VideoSource source, bool creditless, OverlapType overlap = OverlapType.None, bool hasAudio = true)
        => new(id, "b", "f", source, overlap, "https://v/" + id, creditless, hasAudio ? new Audio(id, "p", "f", "audio/ogg", "https://a/" + id) : null!);

    private static AnimeTheme Theme(
        int id,
        string slug,
        ThemeType type = ThemeType.OP,
        string? group = null,
        bool spoiler = false,
        bool creditless = true,
        OverlapType overlap = OverlapType.None,
        VideoSource source = VideoSource.BD,
        int? sequence = null,
        bool hasAudio = true,
        Video[]? videos = null,
        bool nsfw = false)
    {
        var entry = new AnimeThemeEntry(id * 10, 1, null, nsfw, spoiler, new Collection<Video>(videos ?? [Video(id * 100, source, creditless, overlap, hasAudio)]));
        return new AnimeTheme(id, type, sequence, group, slug, [entry]);
    }
}
