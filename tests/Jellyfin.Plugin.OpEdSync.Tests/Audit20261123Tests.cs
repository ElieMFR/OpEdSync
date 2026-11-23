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
using Jellyfin.Plugin.OpEdSync.Configuration;
using Jellyfin.Plugin.OpEdSync.Models;
using Jellyfin.Plugin.OpEdSync.Reports;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// Audit 2026-11-23, findings 1 to 4.
public class Audit20261123Tests
{
    // 1: an ID the API refuses alone (while the others of the batch are answered) was an empty answer, read as
    // "unknown": the next candidate gave the item the theme of an older season or of another site.
    [Fact]
    public async Task AnIdRefusedAlone_DoesNotFallBackOnTheNextCandidate()
    {
        var handler = new Handler(request =>
        {
            using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().Result);
            var ids = body.RootElement.GetProperty("variables").GetProperty("id").EnumerateArray().Select(it => it.GetInt32()).ToArray();
            return ids.Contains(3)
                ? Json("{\"errors\":[{\"message\":\"Something broke.\"}]}")
                : Json(Fixture());
        });
        var downloader = new AnimeThemesDownloader(Unused<IMediaEncoder>(), Unused<ILibraryManager>(), new Factory(handler), NullLogger<AnimeThemesDownloader>.Instance);

        // Refused alone on AniDB, then MAL 1 would find Cowboy Bebop.
        var refused = new Series { Id = Guid.NewGuid(), ProductionYear = 1998 };
        refused.ProviderIds["AniDB"] = "3";
        refused.ProviderIds["MyAnimeList"] = "1";

        // Answered.
        var answered = new Series { Id = Guid.NewGuid(), ProductionYear = 1998 };
        answered.ProviderIds["AniDB"] = "23";
        var report = new RunReport();
        var resolved = new List<ItemWithAnime>();

        await foreach (var it in downloader.ResolveItems([refused, answered], new PluginConfiguration { ThemeSource = ThemeSource.Series }, report, CancellationToken.None))
        {
            resolved.Add(it);
        }

        Assert.Equal(answered.Id, Assert.Single(resolved).Item.Id);
        Assert.Equal(MissingReason.LookupFailed, report.ToSummary(false).Reasons[refused.Id]);
    }

    // 3: GraphQL puts a null in the list for an entry it could not resolve; it threw InvalidOperationException.
    [Fact]
    public void ANullInTheList_IsSkipped()
    {
        using var document = JsonDocument.Parse("{\"data\":{\"filterAnimeByExternalSite\":[null]},\"errors\":[{\"message\":\"x\"}]}");

        var result = AnimeThemesGraphQl.Parse(document);

        Assert.Empty(result.Anime);
        Assert.Single(result.Errors);
    }

    // 4: a pattern that times out on a name aborted the whole run.
    [Fact]
    public void ALibraryPatternThatTimesOut_LeavesTheLibraryOut()
    {
        var selector = new LibrarySelector(Unused<ILibraryManager>(), NullLogger<LibrarySelector>.Instance);
        var library = new Series { Name = new string('a', 40) + "!" };

        var included = selector.IsIncluded(library, new PluginConfiguration { UseLibraryList = false, IncludeLibraries = "^(a+)+$" });

        Assert.False(included);
    }

    // 2: each redirection is checked before it is requested.
    [Fact]
    public async Task ARedirectionOnAnimeThemes_IsFollowed()
    {
        var requested = new List<string>();
        var handler = new Handler(request =>
        {
            requested.Add(request.RequestUri!.AbsoluteUri);
            return request.RequestUri.AbsolutePath == "/a.webm"
                ? Redirect("/b.webm")
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("video") };
        });
        using var client = new HttpClient(handler, false) { BaseAddress = new Uri("https://api.animethemes.moe") };

        using var response = await AnimeThemesDownloader.GetFollowingTrustedRedirects(client, "https://v.animethemes.moe/a.webm", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["https://v.animethemes.moe/a.webm", "https://v.animethemes.moe/b.webm"], requested);
    }

    [Fact]
    public async Task ARedirectionOutsideAnimeThemes_IsNeverRequested()
    {
        var requested = new List<string>();
        var handler = new Handler(request =>
        {
            requested.Add(request.RequestUri!.AbsoluteUri);
            return Redirect("https://evil.example/x.webm");
        });
        using var client = new HttpClient(handler, false) { BaseAddress = new Uri("https://api.animethemes.moe") };

        await Assert.ThrowsAsync<InvalidDataException>(() => AnimeThemesDownloader.GetFollowingTrustedRedirects(client, "https://v.animethemes.moe/a.webm", CancellationToken.None));

        Assert.Single(requested);
    }

    [Fact]
    public async Task EndlessRedirections_AreRefused()
    {
        var handler = new Handler(_ => Redirect("/again.webm"));
        using var client = new HttpClient(handler, false) { BaseAddress = new Uri("https://api.animethemes.moe") };

        await Assert.ThrowsAsync<InvalidDataException>(() => AnimeThemesDownloader.GetFollowingTrustedRedirects(client, "https://v.animethemes.moe/a.webm", CancellationToken.None));
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static HttpResponseMessage Json(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string Fixture()
    {
        using var stream = typeof(Audit20261123Tests).Assembly.GetManifestResourceStream("Jellyfin.Plugin.OpEdSync.Tests.Fixtures.animethemes-lookup-3-shows.json")!;
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
