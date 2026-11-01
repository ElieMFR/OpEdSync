using System;
using System.IO;
using Jellyfin.Plugin.OpEdSync.Reports;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
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
