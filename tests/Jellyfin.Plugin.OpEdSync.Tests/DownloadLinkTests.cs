using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// Audit 2026-11-12: the download link came from the API and was followed as it was, with no size limit.
public class DownloadLinkTests
{
    private static readonly Uri Official = new("https://graphql.animethemes.moe/");
    private static readonly Uri Bench = new("http://127.0.0.1:8099/");

    [Theory]
    [InlineData("https://v.animethemes.moe/CowboyBebop-OP1.webm")]
    [InlineData("https://a.animethemes.moe/CowboyBebop-OP1.ogg")]
    [InlineData("https://animethemes.moe/x.webm")]
    public void AnimeThemesLinks_AreTrusted(string link)
    {
        Assert.True(AnimeThemesDownloader.IsTrustedLink(link, Official));
    }

    [Theory]
    [InlineData("http://v.animethemes.moe/x.webm")]
    [InlineData("https://evil.example/x.webm")]
    [InlineData("https://animethemes.moe.evil.example/x.webm")]
    [InlineData("https://notanimethemes.moe/x.webm")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ftp://v.animethemes.moe/x.webm")]
    [InlineData("/relative/x.webm")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherLinks_AreRefused(string? link)
    {
        Assert.False(AnimeThemesDownloader.IsTrustedLink(link, Official));
    }

    [Fact]
    public void TheConfiguredAddress_IsTrusted_ForTheTestBench()
    {
        Assert.True(AnimeThemesDownloader.IsTrustedLink("http://127.0.0.1:8099/v/x.webm", Bench));
        Assert.False(AnimeThemesDownloader.IsTrustedLink("http://127.0.0.1:9000/v/x.webm", Bench));
        Assert.False(AnimeThemesDownloader.IsTrustedLink("http://127.0.0.1:8099/v/x.webm", Official));
    }

    [Fact]
    public async Task ACopyPastTheLimit_IsRefused()
    {
        using var source = new MemoryStream(new byte[1000]);
        using var target = new MemoryStream();

        await Assert.ThrowsAsync<InvalidDataException>(() => AnimeThemesDownloader.CopyLimitedAsync(source, target, 999, CancellationToken.None));
    }

    [Fact]
    public async Task ACopyWithinTheLimit_IsComplete()
    {
        using var source = new MemoryStream(new byte[1000]);
        using var target = new MemoryStream();

        await AnimeThemesDownloader.CopyLimitedAsync(source, target, 1000, CancellationToken.None);

        Assert.Equal(1000, target.Length);
    }
}
