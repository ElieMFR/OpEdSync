using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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

// 2026-11-08: AnimeThemes retired its REST API (503) in favour of GraphQL. The fixture is a real answer of
// rs.animethemes.moe/graphql to the plugin's own query, recorded the same day.
public class GraphQlTests
{
    private static string Fixture()
    {
        using var stream = typeof(GraphQlTests).Assembly.GetManifestResourceStream("Jellyfin.Plugin.OpEdSync.Tests.Fixtures.animethemes-lookup-3-shows.json")!;
        return new StreamReader(stream).ReadToEnd();
    }

    private static GraphQlResult Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return AnimeThemesGraphQl.Parse(document);
    }

    private static string One(string type = "OP", string overlap = "NONE", string? source = "BD", string audio = "{\"id\":1,\"filename\":\"A-OP1\",\"link\":\"https://a/A-OP1.ogg\",\"mimetype\":\"audio/ogg\",\"path\":\"p\"}")
        => "{\"data\":{\"filterAnimeByExternalSite\":[{\"id\":1,\"slug\":\"a\",\"year\":2020,\"title\":{\"romaji\":\"A\"},\"resources\":{\"pageInfo\":{\"hasNextPage\":false},\"nodes\":[]},"
           + "\"themes\":[{\"id\":10,\"type\":\"" + type + "\",\"sequence\":1,\"slug\":\"OP1\",\"entries\":[{\"id\":100,\"version\":1,\"episodes\":\"1-3\",\"nsfw\":false,\"spoiler\":false,"
           + "\"videos\":{\"nodes\":[{\"id\":1000,\"basename\":\"A-OP1.webm\",\"filename\":\"A-OP1\",\"link\":\"https://v/A-OP1.webm\",\"nc\":true,\"overlap\":\"" + overlap + "\",\"source\":"
           + (source is null ? "null" : "\"" + source + "\"") + ",\"audio\":" + audio + "}]}}]}]}]}}";

    [Fact]
    public void RealAnswer_IsReadIntoTheModel()
    {
        var result = Read(Fixture());

        Assert.Empty(result.Errors);
        Assert.Equal(0, result.TruncatedResources);
        Assert.Equal([521, 1510, 1566], result.Anime.Select(it => it.Id));

        var bebop = result.Anime[0];
        Assert.Equal("Cowboy Bebop", bebop.Name);
        Assert.Equal("cowboy_bebop", bebop.Slug);
        Assert.Equal(1998, bebop.Year);
        Assert.Equal(23, bebop.AniDbId);
        Assert.Equal(1, bebop.GetExternalId(Sites.MYANIMELIST));
        Assert.Equal(1, bebop.GetExternalId(Sites.ANILIST));
        Assert.Equal(4, bebop.Themes.Count);

        var ed1 = bebop.Themes.Single(it => it.Slug == "ED1");
        Assert.Equal(ThemeType.ED, ed1.Type);
        var video = ed1.Entries.Single().Videos.Single();
        Assert.True(video.Creditless);
        Assert.Equal(OverlapType.None, video.Overlap);
        Assert.Equal(VideoSource.BD, video.Source);
        Assert.Equal("CowboyBebop-ED1", video.Audio.Filename);
        Assert.Equal("https://a.animethemes.moe/CowboyBebop-ED1.ogg", video.Audio.Link);
    }

    // The API says TRANS: read as a plain "Over", the themes with a short transition would be filtered out.
    [Fact]
    public void RealAnswer_ReadsTransAsTransition_AndSpoilers()
    {
        var ed2 = Read(Fixture()).Anime[0].Themes.Single(it => it.Slug == "ED2");

        Assert.Equal(OverlapType.Transition, ed2.Entries.Single().Videos.Single().Overlap);
        Assert.True(ed2.Entries.Single().Spoiler);
    }

    [Theory]
    [InlineData("NONE", OverlapType.None)]
    [InlineData("TRANS", OverlapType.Transition)]
    [InlineData("OVER", OverlapType.Over)]
    [InlineData("SOMETHING_NEW", OverlapType.Over)]
    public void Overlap_IsMapped_AndAnUnknownOneIsTheMostCautious(string value, OverlapType expected)
    {
        Assert.Equal(expected, Read(One(overlap: value)).Anime[0].Themes[0].Entries[0].Videos[0].Overlap);
    }

    [Theory]
    [InlineData("OP", ThemeType.OP)]
    [InlineData("ED", ThemeType.ED)]
    [InlineData("IN", ThemeType.Other)]
    [InlineData("SOMETHING_NEW", ThemeType.Other)]
    public void ThemeType_IsMapped_AndInsertSongsAreOther(string value, ThemeType expected)
    {
        Assert.Equal(expected, Read(One(type: value)).Anime[0].Themes[0].Type);
    }

    [Theory]
    [InlineData("BD", VideoSource.BD)]
    [InlineData("web", VideoSource.WEB)]
    [InlineData("BLURAY4K", VideoSource.WEB)]
    [InlineData("1", VideoSource.WEB)]
    public void Source_IsMapped_AndAnUnknownOneCountsAsWeb(string value, VideoSource expected)
    {
        Assert.Equal(expected, Read(One(source: value)).Anime[0].Themes[0].Entries[0].Videos[0].Source);
    }

    [Fact]
    public void VideoWithoutAudio_HasNoAudio_AndAnUnknownSourceIsNull()
    {
        var video = Read(One(audio: "null", source: null)).Anime[0].Themes[0].Entries[0].Videos[0];

        Assert.Null(video.Audio);
        Assert.Null(video.Source);
    }

    [Fact]
    public void OnlyTheSitesThePluginUnderstandsAreKept()
    {
        var json = "{\"data\":{\"filterAnimeByExternalSite\":[{\"id\":1,\"slug\":\"a\",\"year\":null,\"title\":{\"romaji\":\"A\"},"
                   + "\"resources\":{\"pageInfo\":{\"hasNextPage\":true},\"nodes\":[{\"id\":1,\"externalId\":7,\"site\":\"ANIDB\"},{\"id\":2,\"externalId\":8,\"site\":\"MAL\"},"
                   + "{\"id\":3,\"externalId\":null,\"site\":\"ANILIST\"},{\"id\":4,\"externalId\":9,\"site\":\"NETFLIX\"}]},\"themes\":[]}]}}";

        var result = Read(json);

        Assert.Equal([Sites.ANIDB, Sites.MYANIMELIST], result.Anime[0].Resources.Select(it => it.Site));
        Assert.Null(result.Anime[0].Year);
        Assert.Equal(1, result.TruncatedResources);
    }

    // GraphQL answers errors (too complex, too deep) with HTTP 200: they must come out as errors, not as "nothing found".
    [Fact]
    public void ErrorsOfAnHttp200_AreReported()
    {
        var result = Read("{\"errors\":[{\"message\":\"Query is too complex.\"}]}");

        Assert.Equal(["Query is too complex."], result.Errors);
        Assert.Empty(result.Anime);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":{\"filterAnimeByExternalSite\":[{\"slug\":\"no-id\"}]}}")]
    public void AnAnswerWithoutWhatTheApiDocuments_IsAJsonException(string json)
    {
        Assert.Throws<JsonException>(() => Read(json));
    }

    [Fact]
    public void LookupRequest_CarriesTheSiteAndTheIdsAsVariables()
    {
        using var document = JsonDocument.Parse(AnimeThemesGraphQl.BuildLookupRequest(Sites.MYANIMELIST, [5, 6]));
        var root = document.RootElement;

        Assert.Equal(AnimeThemesGraphQl.LookupQuery, root.GetProperty("query").GetString());
        Assert.Equal("MAL", root.GetProperty("variables").GetProperty("site").GetString());
        Assert.Equal([5, 6], root.GetProperty("variables").GetProperty("id").EnumerateArray().Select(it => it.GetInt32()));
        Assert.Equal("ANIDB", AnimeThemesGraphQl.ToGraphQlSite(Sites.ANIDB));
        Assert.Equal("ANILIST", AnimeThemesGraphQl.ToGraphQlSite(Sites.ANILIST));
        Assert.Throws<ArgumentOutOfRangeException>(() => AnimeThemesGraphQl.ToGraphQlSite("Netflix"));
    }

    [Fact]
    public async Task Lookup_PostsTheQuery_AndMatchesEachAnimeToItsId()
    {
        var handler = new ScriptedHandler(_ => Json(Fixture()));
        var api = NewApi(handler);

        var found = await api.FindByExternalId(Sites.ANIDB, [23, 10937, 11829, 999999999], CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.animethemes.test/", request.Uri);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("ANIDB", body.RootElement.GetProperty("variables").GetProperty("site").GetString());
        Assert.Equal([23, 10937, 11829, 999999999], body.RootElement.GetProperty("variables").GetProperty("id").EnumerateArray().Select(it => it.GetInt32()));

        Assert.Equal("Cowboy Bebop", Assert.Single(found[23]).Name);
        Assert.Equal("Koe no Katachi", Assert.Single(found[10937]).Name);
        Assert.Equal("Kimi no Na wa.", Assert.Single(found[11829]).Name);
        Assert.Empty(found[999999999]);
    }

    [Fact]
    public async Task ABatchTheApiFindsTooComplex_IsSplitInTwo()
    {
        var handler = new ScriptedHandler(request =>
        {
            using var body = JsonDocument.Parse(request.Body);
            var count = body.RootElement.GetProperty("variables").GetProperty("id").GetArrayLength();
            return count > 2
                ? Json("{\"errors\":[{\"message\":\"Query is too complex.\"}]}")
                : Json("{\"data\":{\"filterAnimeByExternalSite\":[]}}");
        });
        var api = NewApi(handler);

        var found = await api.FindByExternalId(Sites.ANIDB, [1, 2, 3, 4], CancellationToken.None);

        Assert.Equal([4, 2, 2], handler.Requests.Select(it => JsonDocument.Parse(it.Body).RootElement.GetProperty("variables").GetProperty("id").GetArrayLength()));
        Assert.Equal(4, found.Count);
    }

    [Fact]
    public async Task AnyOtherError_FailsTheLookup()
    {
        var api = NewApi(new ScriptedHandler(_ => Json("{\"errors\":[{\"message\":\"Something broke.\"}]}")));

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => api.FindByExternalId(Sites.ANIDB, [1, 2], CancellationToken.None).AsTask());

        Assert.Contains("Something broke.", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServerError_FailsTheLookup()
    {
        var api = NewApi(new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<HttpRequestException>(() => api.FindByExternalId(Sites.ANIDB, [1], CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task MoreThan100Ids_AreRefused()
    {
        var api = NewApi(new ScriptedHandler(_ => Json("{}")));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => api.FindByExternalId(Sites.ANIDB, Enumerable.Range(1, 101), CancellationToken.None).AsTask());
    }

    // A large library searches for minutes: the lookups must slow down before the 90 requests a minute are used up.
    [Fact]
    public async Task TheLimitHeaders_FeedTheGate()
    {
        var gate = new RateLimitGate();
        var reset = DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds();
        var handler = new ScriptedHandler(_ =>
        {
            var response = Json("{\"data\":{\"filterAnimeByExternalSite\":[]}}");
            response.Headers.Add("x-ratelimit-remaining", "2");
            response.Headers.Add("x-ratelimit-reset", reset.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return response;
        });
        var api = new AnimeThemesApi(new Factory(handler), NullLogger<AnimeThemesDownloader>.Instance, gate);

        await api.FindByExternalId(Sites.ANIDB, [1], CancellationToken.None);

        var delay = gate.DelayAt(DateTimeOffset.UtcNow);
        Assert.InRange(delay, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(32));
    }

    [Fact]
    public async Task TheStatusCheck_PostsTheMinimalQuery()
    {
        var handler = new ScriptedHandler(_ => Json("{\"data\":{\"__typename\":\"Query\"}}"));
        using var service = new StatusService(NoPlugins(), new Factory(handler));

        var status = await service.GetAsync(true, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("__typename", request.Body, StringComparison.Ordinal);
        Assert.True(status.AnimeThemes!.Reachable);
    }

    private static AnimeThemesApi NewApi(ScriptedHandler handler)
        => new(new Factory(handler), NullLogger<AnimeThemesDownloader>.Instance, new RateLimitGate());

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static IPluginManager NoPlugins()
        => System.Reflection.DispatchProxy.Create<IPluginManager, NoPluginsProxy>();

    public class NoPluginsProxy : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.Name == "get_Plugins" ? new List<LocalPlugin>() : throw new NotSupportedException(targetMethod?.Name);
    }

    private sealed record Sent(HttpMethod Method, string Uri, string Body);

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false) { BaseAddress = new Uri("https://api.animethemes.test") };
    }

    private sealed class ScriptedHandler(Func<Sent, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Sent> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var sent = new Sent(request.Method, request.RequestUri!.ToString(), body);
            Requests.Add(sent);
            return answer(sent);
        }
    }
}
