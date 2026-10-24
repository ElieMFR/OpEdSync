using System;
using Jellyfin.Plugin.OpEdSync.Reports;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

public class LocalizationAndReportTests
{
    [Theory]
    [InlineData("fr", true)]
    [InlineData("fr-FR", true)]
    [InlineData("FR-ca", true)]
    [InlineData("en-US", false)]
    [InlineData("de", false)]
    [InlineData("fra", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsFrench_OnlyForFrenchCultures(string? culture, bool expected)
    {
        Assert.Equal(expected, Localization.IsFrench(culture));
        Assert.Equal(expected ? "fr" : "en", Localization.Pick(culture, "fr", "en"));
    }

    [Fact]
    public void RunReport_KeepsTheMostInformativeReason()
    {
        var report = new RunReport();
        var id = Guid.NewGuid();

        report.SetReason(id, MissingReason.Filtered);
        report.SetReason(id, MissingReason.NotOnAnimeThemes);

        Assert.Equal(MissingReason.Filtered, report.ToSummary(false).Reasons[id]);
    }

    [Fact]
    public void RunReport_ForgetsItemsThatGotATheme()
    {
        var report = new RunReport();
        var id = Guid.NewGuid();

        report.SetReason(id, MissingReason.NoVideo);
        report.ClearReason(id);
        report.CountAdded();

        var summary = report.ToSummary(false);
        Assert.False(summary.Reasons.ContainsKey(id));
        Assert.Equal(1, summary.Added);
    }
}
