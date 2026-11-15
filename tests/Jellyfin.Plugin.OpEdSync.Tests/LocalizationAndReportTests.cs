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

    [Fact]
    public void ConfigurationPage_SaysWhenTheFiguresCouldNotBeLoaded_InBothLanguages()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.OpEdSync.Configuration.configPage.html")!;
        var html = new System.IO.StreamReader(stream).ReadToEnd();

        Assert.Contains("\"statsFailed\": \"Les chiffres n'ont pas pu être chargés. Rechargez la page.\"", html, StringComparison.Ordinal);
        Assert.Contains("\"statsFailed\": \"The figures could not be loaded. Reload the page.\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"opsStatsError\"", html, StringComparison.Ordinal);

        // The figures show a dash, not the placeholder or zeros, and the message is shown by the failing load itself.
        Assert.Contains("showStatsFailed()", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationPage_SaysTheYearCheckSkipsASeasonWithItsOwnId_InBothLanguages()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.OpEdSync.Configuration.configPage.html")!;
        var html = new System.IO.StreamReader(stream).ReadToEnd();

        Assert.Contains("une saison qui a son propre numéro d’animé n’est pas vérifiée.", html, StringComparison.Ordinal);
        Assert.Contains("a season that has an anime number of its own is not checked.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Ça évite la plupart des identifiants erronés", html, StringComparison.Ordinal);
    }

    // Two rules styled classes no element of the page carries (the library list is .ops-lib-list since the filter was added).
    [Fact]
    public void ConfigurationPage_HasNoStyleRuleForClassesItNeverUses()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.OpEdSync.Configuration.configPage.html")!;
        var html = new System.IO.StreamReader(stream).ReadToEnd();

        Assert.DoesNotContain(".ops-title", html, StringComparison.Ordinal);
        Assert.DoesNotContain(".ops-libraries", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"ops-title", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"ops-libraries", html, StringComparison.Ordinal);

        // Its other rules stay.
        Assert.Contains("#OpEdSyncConfigPage .ops-lib-list {", html, StringComparison.Ordinal);
        Assert.Contains("#OpEdSyncConfigPage .ops-grid { grid-template-columns: minmax(0, 1fr); }", html, StringComparison.Ordinal);
        Assert.Contains("#OpEdSyncConfigPage .ops-cards, #OpEdSyncConfigPage .ops-actions { grid-template-columns: minmax(0, 1fr); }", html, StringComparison.Ordinal);
        Assert.Contains(".ops-section-title", html, StringComparison.Ordinal);
    }
}
