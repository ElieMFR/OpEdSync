using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

/// <summary>
/// Which lookups the plugin tries for a series, and with which year to check. Real data of a Pokémon series on the
/// reference server: TVDB dates its seasons wrongly (the season of "Pokémon Horizons" is dated 2016).
/// </summary>
public class CandidateTests
{
    private static (string Site, int Id, int? Year) Anidb(int id, int? year) => ("AniDB", id, year);

    [Fact]
    public void ASeasonWithAnIdOfItsOwn_IsNotCheckedAgainstItsYear()
    {
        // Seasons 20 to 18 and 16 of the Pokémon series, newest first, and the series itself.
        var seasons = new[] { Anidb(17777, 2016), Anidb(15067, 2016), Anidb(12407, 2014), Anidb(9956, 2012) };
        var series = new[] { Anidb(230, 1997) };

        var candidates = AnimeThemesDownloader.BuildCandidates(seasons, series);

        Assert.Equal(new (string, int, int?)[] { Anidb(17777, null), Anidb(15067, null), Anidb(12407, null), Anidb(9956, null), Anidb(230, 1997) }, candidates);
    }

    [Fact]
    public void AnIdSharedByManySeasons_KeepsItsYears()
    {
        // Seasons 10 to 13 of the same series all carry 4728: one anime for four seasons, the year tells which one is right.
        var seasons = new[] { Anidb(4728, 2010), Anidb(4728, 2009), Anidb(4728, 2008), Anidb(4728, 2007) };

        var candidates = AnimeThemesDownloader.BuildCandidates(seasons, [Anidb(230, 1997)]);

        Assert.Equal(4, candidates.Count(it => it.Id == 4728));
        Assert.All(candidates.Where(it => it.Id == 4728), it => Assert.NotNull(it.Year));
    }

    [Fact]
    public void TheIdOfTheSeries_KeepsItsYear_EvenOnASeason()
    {
        // A wrong id: the season and the series both point at an older anime. The year is what rejects it.
        var seasons = new[] { Anidb(4776, 2026) };

        var candidates = AnimeThemesDownloader.BuildCandidates(seasons, [Anidb(4776, 2025)]);

        Assert.All(candidates, it => Assert.NotNull(it.Year));
    }

    [Fact]
    public void ASingleSeasonWithAnotherIdThanTheSeries_IsNotCheckedAgainstItsYear()
    {
        var candidates = AnimeThemesDownloader.BuildCandidates([Anidb(18090, 2023)], [Anidb(14000, 2023)]);

        Assert.Equal(Anidb(18090, null), candidates[0]);
        Assert.Equal(Anidb(14000, 2023), candidates[1]);
    }

    [Fact]
    public void WithoutSeasons_OnlyTheSeriesIsTried()
    {
        var candidates = AnimeThemesDownloader.BuildCandidates([], [Anidb(230, 1997)]);

        Assert.Equal(new (string, int, int?)[] { Anidb(230, 1997) }, candidates);
    }
}
