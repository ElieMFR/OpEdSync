using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

public class ThemeFileRulesTests
{
    // Audit point 1: the configuration page allowed 0, which ParallelOptions rejects on every run.
    [Fact]
    public void ParallelOptions_RejectsZero_WhichIsWhyTheValueIsClamped()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParallelOptions { MaxDegreeOfParallelism = 0 });
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(99, 8)]
    public void ClampParallelism_KeepsAValidValue(int configured, int expected)
    {
        Assert.Equal(expected, ThemeFileRules.ClampParallelism(configured));
        _ = new ParallelOptions { MaxDegreeOfParallelism = ThemeFileRules.ClampParallelism(configured) };
    }

    // Audit point 2: a user supplied theme.mp3 used to be deleted when "forced sync" was on.
    [Fact]
    public void FilesToRemove_NeverSelectsFilesThePluginDidNotWrite()
    {
        var existing = new[] { "/a/theme-music/theme.mp3", "/a/theme-music/my song.mp3", "/a/theme-music/Bleach-OP9__50.mp3", "/a/theme-music/Bleach2022-OP1__50.mp3" };

        var removed = ThemeFileRules.FilesToRemove(existing, ["Bleach2022-OP1__50.mp3"]).ToList();

        Assert.Equal(["/a/theme-music/Bleach-OP9__50.mp3"], removed);
    }

    [Fact]
    public void FilesToRemove_WithNothingToKeep_StillSparesUserFiles()
    {
        var removed = ThemeFileRules.FilesToRemove(["theme.mp3", "Show-OP1__50.mp3"], []).ToList();

        Assert.Equal(["Show-OP1__50.mp3"], removed);
    }

    // Audit point 3: changing the volume had no effect on themes already on disk.
    [Fact]
    public void HasOutdatedVolume_DetectsAnotherVolume()
    {
        Assert.True(ThemeFileRules.HasOutdatedVolume(["Show-OP1__50.mp3"], 0.8));
        Assert.False(ThemeFileRules.HasOutdatedVolume(["Show-OP1__80.mp3"], 0.8));
        Assert.False(ThemeFileRules.HasOutdatedVolume(["theme.mp3"], 0.8));
        Assert.False(ThemeFileRules.HasOutdatedVolume([], 0.8));
    }

    [Fact]
    public void SupersededVariants_SelectsTheSameThemeAtAnotherVolumeOnly()
    {
        var existing = new[] { "/d/Show-OP1__50.mp3", "/d/Show-OP1__80.mp3", "/d/Show-ED1__50.mp3", "/d/theme.mp3" };

        var superseded = ThemeFileRules.SupersededVariants(existing, "/d/Show-OP1__80.mp3").ToList();

        Assert.Equal(["/d/Show-OP1__50.mp3"], superseded);
    }

    // File names must stay identical to the ones written by earlier versions ({volume * 100:0}).
    [Theory]
    [InlineData(0.5, "Show-OP1__50.mp3")]
    [InlineData(0.55, "Show-OP1__55.mp3")]
    [InlineData(0.05, "Show-OP1__5.mp3")]
    [InlineData(0.0, "Show-OP1__0.mp3")]
    [InlineData(1.0, "Show-OP1__100.mp3")]
    public void BuildFileName_MatchesEarlierVersions(double volume, string expected)
    {
        Assert.Equal(expected, ThemeFileRules.BuildFileName("Show-OP1", volume, "mp3"));
        Assert.True(ThemeFileRules.IsPluginFile(expected));
    }
}
