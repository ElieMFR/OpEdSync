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

    // Audit 2026-11-12: a run that crashed was saved as "cancelled", so the page blamed the user.
    [Fact]
    public void ARunThatCrashed_IsNotSavedAsCancelled()
    {
        Assert.Equal((false, false), Tasks.BaseThemeSearchTask.EndOf(null));
        Assert.Equal((true, false), Tasks.BaseThemeSearchTask.EndOf(new OperationCanceledException()));
        Assert.Equal((true, false), Tasks.BaseThemeSearchTask.EndOf(new System.Threading.Tasks.TaskCanceledException()));
        Assert.Equal((false, true), Tasks.BaseThemeSearchTask.EndOf(new InvalidOperationException()));

        var summary = new RunReport().ToSummary(false, true);
        Assert.True(summary.Crashed);
        Assert.False(summary.Cancelled);
    }

    [Fact]
    public void ConfigurationPage_SaysWhenARunCrashed_InBothLanguages()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.OpEdSync.Configuration.configPage.html")!;
        var html = new System.IO.StreamReader(stream).ReadToEnd();

        Assert.Equal(2, html.Split("\"crashed\":").Length - 1);
        Assert.Contains("run.Crashed", html, StringComparison.Ordinal);
    }
}
