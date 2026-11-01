using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.OpEdSync.Api;
using Jellyfin.Plugin.OpEdSync.Models;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// Audit 2026-11-01, points 2 to 12.
public class AuditFixesTests
{
    private const string AnimeJson = """
        {"anime":[{"id":1,"name":"A","year":2020,"resources":[],"animethemes":[{"id":10,"type":"__TYPE__","sequence":1,"animethemeentries":[{"id":100,"nsfw":false,"spoiler":false,"videos":[{"id":1000,"basename":"A-OP1.webm","filename":"A-OP1","source":"__SOURCE__","overlap":"__OVERLAP__","link":"https://v/A-OP1.webm","nc":true,"audio":null}]}]}]}],"links":{"next":null}}
        """;

    private static AnimeResponse? Parse(string type, string source = "BD", string overlap = "None")
        => JsonSerializer.Deserialize<AnimeResponse>(AnimeJson.Replace("__TYPE__", type, StringComparison.Ordinal).Replace("__SOURCE__", source, StringComparison.Ordinal).Replace("__OVERLAP__", overlap, StringComparison.Ordinal));

    // Point 2: a type added later by AnimeThemes (insert songs, for instance) made the whole page unreadable.
    [Fact]
    public void UnknownEnumValues_DoNotFailTheResponse()
    {
        var theme = Parse("IN", "BLURAY4K", "Partial")!.Anime[0].Themes[0];

        Assert.Equal(ThemeType.Other, theme.Type);
        Assert.Equal(VideoSource.WEB, theme.Entries[0].Videos[0].Source);
        Assert.Equal(OverlapType.Over, theme.Entries[0].Videos[0].Overlap);
    }

    [Fact]
    public void KnownEnumValues_AreStillRead()
    {
        var theme = Parse("ED", "BD", "Transition")!.Anime[0].Themes[0];

        Assert.Equal(ThemeType.ED, theme.Type);
        Assert.Equal(VideoSource.BD, theme.Entries[0].Videos[0].Source);
        Assert.Equal(OverlapType.Transition, theme.Entries[0].Videos[0].Overlap);
    }

    // Point 2: a maintenance page served with 200 is a JsonException, which the lookup did not catch.
    [Fact]
    public async Task MaintenancePage_IsAJsonException()
    {
        var factory = new FakeFactory(new RespondingHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Down for maintenance</html>", Encoding.UTF8, "text/html"),
        }));
        using var api = new AnimeThemesApi(factory, NullLogger<AnimeThemesDownloader>.Instance);

        await Assert.ThrowsAsync<JsonException>(() => api.FindByExternalId(Sites.ANIDB, [1], CancellationToken.None).AsTask());
    }

    // Point 5: after a volume change, an old file whose theme is no longer the best one was never removed.
    [Fact]
    public void OutdatedVolumeFiles_IncludesAThemeThatChangedName()
    {
        var existing = new[] { "/a/theme-music/Show-OP1__50.mp3", "/a/theme-music/Show-OP2__70.mp3", "/a/theme-music/theme.mp3" };

        Assert.Equal(["/a/theme-music/Show-OP1__50.mp3"], ThemeFileRules.OutdatedVolumeFiles(existing, 0.7).ToList());
        Assert.Empty(ThemeFileRules.SupersededVariants(existing, "/a/theme-music/Show-OP2__70.mp3"));
    }

    // Point 4: files on disk that Jellyfin does not list yet must trigger a refresh.
    [Theory]
    [InlineData(new[] { "/a/backdrops/Show-OP1__100.webm" }, false, true)]
    [InlineData(new[] { "/a/backdrops/Show-OP1__100.webm" }, true, false)]
    [InlineData(new[] { "/a/backdrops/mine.webm" }, false, false)]
    [InlineData(new string[0], false, false)]
    public void NeedsRegistration_OnlyForPluginFilesUnknownToJellyfin(string[] existing, bool known, bool expected)
    {
        Assert.Equal(expected, ThemeFileRules.NeedsRegistration(existing, known));
    }

    // Point 12: a name sent by the API must never point outside the theme folder.
    [Theory]
    [InlineData("Show-OP1", "Show-OP1")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\Windows\\evil", "evil")]
    [InlineData("/abs/path/Show-ED1", "Show-ED1")]
    [InlineData("..", null)]
    [InlineData("a/..", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void SafeBaseName_KeepsOnlyTheFileName(string? name, string? expected)
    {
        Assert.Equal(expected, ThemeFileRules.SafeBaseName(name));
    }

    [Fact]
    public void SafeBaseName_NeverLeavesTheFolder()
    {
        var folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "show", "theme-music"));
        foreach (var name in new[] { "../../x", "..\\..\\x", "/x", "C:\\x" })
        {
            var full = Path.GetFullPath(Path.Combine(folder, ThemeFileRules.BuildFileName(ThemeFileRules.SafeBaseName(name)!, 1, "mp3")));
            Assert.Equal(folder, Path.GetDirectoryName(full));
        }
    }

    // Point 6: a check cancelled by a closed page was kept for 10 minutes as "AnimeThemes does not answer".
    [Fact]
    public async Task CancelledCheck_IsNotKept()
    {
        var answer = false;
        var factory = new FakeFactory(new RespondingHandler(() => answer ? new HttpResponseMessage(HttpStatusCode.OK) : null));
        using var service = new StatusService(EmptyPluginManager(), factory);

        using var cts = new CancellationTokenSource();
        var cancelled = service.GetAsync(false, cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

        answer = true;
        var status = await service.GetAsync(false, CancellationToken.None);

        Assert.True(status.AnimeThemes!.Reachable);
    }

    private static IPluginManager EmptyPluginManager()
    {
        var proxy = DispatchProxy.Create<IPluginManager, EmptyPluginManagerProxy>();
        return proxy;
    }

    public class EmptyPluginManagerProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.Name == "get_Plugins" ? new List<LocalPlugin>() : throw new NotSupportedException(targetMethod?.Name);
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(handler, false) { BaseAddress = new Uri("https://api.animethemes.test") };
    }

    // Answers with the given response, or waits until the request is cancelled when it gives none.
    private sealed class RespondingHandler(Func<HttpResponseMessage?> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = respond();
            if (response is null)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return response!;
        }
    }
}
