using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.OpEdSync.Configuration;
using Jellyfin.Plugin.OpEdSync.Models;
using Jellyfin.Plugin.OpEdSync.Reports;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

public class LookupFailureTests
{
    // Audit 2026-11-12: after a failed lookup on the preferred ID, the next round took the next ID (an older season,
    // or another site) and the item got the theme of something else, with no mention of the failure.
    [Fact]
    public async Task AFailedLookup_DoesNotFallBackOnTheNextId()
    {
        var calls = 0;
        var handler = new Handler(_ =>
        {
            // The first lookup (AniDB) fails; the next one (MAL) would find Cowboy Bebop.
            return Interlocked.Increment(ref calls) == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Fixture(), Encoding.UTF8, "application/json") };
        });
        var downloader = new AnimeThemesDownloader(Unused<IMediaEncoder>(), Unused<ILibraryManager>(), new Factory(handler), NullLogger<AnimeThemesDownloader>.Instance);
        var series = new Series { Id = Guid.NewGuid(), ProductionYear = 1998 };
        series.ProviderIds["AniDB"] = "23";
        series.ProviderIds["MyAnimeList"] = "1";
        var report = new RunReport();
        var resolved = new List<ItemWithAnime>();

        await foreach (var it in downloader.ResolveItems([series], new PluginConfiguration { ThemeSource = ThemeSource.Series }, report, CancellationToken.None))
        {
            resolved.Add(it);
        }

        Assert.Empty(resolved);
        Assert.Equal(MissingReason.LookupFailed, report.ToSummary(false).Reasons[series.Id]);
        Assert.Equal(1, calls);
    }

    private static string Fixture()
    {
        using var stream = typeof(LookupFailureTests).Assembly.GetManifestResourceStream("Jellyfin.Plugin.OpEdSync.Tests.Fixtures.animethemes-lookup-3-shows.json")!;
        return new StreamReader(stream).ReadToEnd();
    }

    private static T Unused<T>()
        where T : class
        => DispatchProxy.Create<T, UnusedProxy>();

    public class UnusedProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new NotSupportedException(targetMethod?.Name);
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false) { BaseAddress = new Uri("https://api.animethemes.test") };
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(answer(request));
    }
}
