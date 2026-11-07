using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using Jellyfin.Plugin.OpEdSync.Api;
using Jellyfin.Plugin.OpEdSync.Configuration;
using Jellyfin.Plugin.OpEdSync.Reports;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// Found on the local server (Jellyfin 12.4, 2026-11-01): a movie sharing its folder with other movies gets no theme
// from Jellyfin, so the plugin downloaded for nothing and processed it again on every run.
public class SharedFolderTests
{
    [Fact]
    public void MovieSharingItsFolder_HasNoThemeFolderOfItsOwn()
    {
        Assert.False(AnimeThemesDownloader.HasOwnThemeFolder(new Movie { IsInMixedFolder = true }));
    }

    [Fact]
    public void MovieInItsOwnFolder_AndSeries_HaveTheirThemeFolder()
    {
        Assert.True(AnimeThemesDownloader.HasOwnThemeFolder(new Movie { IsInMixedFolder = false }));
        Assert.True(AnimeThemesDownloader.HasOwnThemeFolder(new Series()));
    }

    // EXG-007: the run itself must skip the movie, not only the rule. "Update existing themes" short-circuits the
    // checks that need a library, so only the kind and the folder decide.
    [Fact]
    public void Run_SkipsAMovieSharingItsFolder()
    {
        var downloader = new AnimeThemesDownloader(Unused<IMediaEncoder>(), Unused<ILibraryManager>(), Unused<IHttpClientFactory>(), NullLogger<AnimeThemesDownloader>.Instance);
        var configuration = new PluginConfiguration { ForceSync = true };

        Assert.False(downloader.ShouldUpdate(new Movie { IsInMixedFolder = true }, configuration));
        Assert.True(downloader.ShouldUpdate(new Movie { IsInMixedFolder = false }, configuration));
        Assert.True(downloader.ShouldUpdate(new Series(), configuration));
    }

    // EXG-042: the page classifies it as "shared folder" before anything else, without asking for an ID.
    [Fact]
    public void Page_ClassifiesAMovieSharingItsFolder_BeforeAnyOtherReason()
    {
        var movie = new Movie { Id = Guid.NewGuid(), IsInMixedFolder = true };
        var lastRun = new RunSummary { Reasons = { [movie.Id] = MissingReason.DownloadFailed } };

        Assert.Equal(MissingReason.SharedFolder, StatsService.ClassifyMissing(movie, () => throw new InvalidOperationException("not asked"), lastRun));
    }

    [Fact]
    public void Page_ClassifiesOtherItems_ByIdThenLastRun()
    {
        var series = new Series { Id = Guid.NewGuid() };
        var lastRun = new RunSummary { Reasons = { [series.Id] = MissingReason.NoVideo } };

        Assert.Equal(MissingReason.NoId, StatsService.ClassifyMissing(series, () => false, lastRun));
        Assert.Equal(MissingReason.NoVideo, StatsService.ClassifyMissing(series, () => true, lastRun));
        Assert.Equal(MissingReason.NotAnalysed, StatsService.ClassifyMissing(series, () => true, null));
    }

    private static T Unused<T>()
        where T : class
        => DispatchProxy.Create<T, UnusedProxy>();

    public class UnusedProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new NotSupportedException(targetMethod?.Name);
    }

    // Every reason the server reports needs a label in both languages of the configuration page.
    [Fact]
    public void ConfigurationPage_LabelsEveryReason()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.OpEdSync.Configuration.configPage.html")!;
        var html = new StreamReader(stream).ReadToEnd();

        foreach (var reason in Enum.GetNames<MissingReason>())
        {
            Assert.Equal(2, html.Split($"\"{reason}\":").Length - 1);
            Assert.Contains($"'{reason}'", html, StringComparison.Ordinal);
        }
    }
}
