using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// EXG-018: a request is tried again at most 3 times on 408, 429 or 5xx, waiting as long as Retry-After asks.
public class RetryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    // Kit audit 2026-11-01: a Retry-After sent as a date was ignored, so the plugin retried at once.
    [Fact]
    public void RetryAfterAsADate_IsRead()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), PollyResilienceHandler.RetryAfterDelay(new RetryConditionHeaderValue(Now.AddSeconds(30)), Now));
    }

    [Fact]
    public void RetryAfterInSeconds_IsRead()
    {
        Assert.Equal(TimeSpan.FromSeconds(7), PollyResilienceHandler.RetryAfterDelay(new RetryConditionHeaderValue(TimeSpan.FromSeconds(7)), Now));
    }

    // Audit 2026-11-12: a huge Retry-After froze the run for as long as the server asked.
    [Fact]
    public void RetryAfter_IsCappedLikeTheRateLimitGate()
    {
        Assert.Equal(RateLimitGate.MaxWait, PollyResilienceHandler.RetryAfterDelay(new RetryConditionHeaderValue(TimeSpan.FromHours(1)), Now));
        Assert.Equal(RateLimitGate.MaxWait, PollyResilienceHandler.RetryAfterDelay(new RetryConditionHeaderValue(Now.AddDays(1)), Now));
    }

    // Audit 2026-11-12: the answers given up on were never released.
    [Fact]
    public async Task AnswersGivenUpOn_AreDisposedBeforeTheNextAttempt()
    {
        var inner = new DisposalTrackingHandler();
        using var client = new HttpClient(new PollyResilienceHandler { InnerHandler = inner });

        using var response = await client.GetAsync(new Uri("http://animethemes.test/anime"));

        Assert.Equal(4, inner.Responses.Count);
        Assert.All(inner.Responses.Take(3), r => Assert.True(r.Disposed));
    }

    private sealed class TrackedContent : HttpContent
    {
        public bool Disposed { get; private set; }

        protected override Task SerializeToStreamAsync(System.IO.Stream stream, System.Net.TransportContext? context) => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class DisposalTrackingHandler : HttpMessageHandler
    {
        public System.Collections.Generic.List<TrackedContent> Responses { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new TrackedContent();
            Responses.Add(content);
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = content };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void RetryAfterInThePast_MeansNoWait_AndNoHeaderMeansBackoff()
    {
        Assert.Equal(TimeSpan.Zero, PollyResilienceHandler.RetryAfterDelay(new RetryConditionHeaderValue(Now.AddSeconds(-5)), Now));
        Assert.Null(PollyResilienceHandler.RetryAfterDelay(null, Now));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task TransientError_IsTriedAgainThreeTimes(HttpStatusCode status)
    {
        var inner = new CountingHandler(status);
        using var client = new HttpClient(new PollyResilienceHandler { InnerHandler = inner });

        using var response = await client.GetAsync(new Uri("http://animethemes.test/anime"));

        Assert.Equal(4, inner.Calls);
        Assert.Equal(status, response.StatusCode);
    }

    // GraphQL lookups are POSTs: every new attempt must carry the same body.
    [Fact]
    public async Task APostWithABody_IsSentAgainWithTheSameBody()
    {
        var inner = new BodyRecordingHandler();
        using var client = new HttpClient(new PollyResilienceHandler { InnerHandler = inner });
        using var content = new StringContent("{\"query\":\"{ __typename }\"}", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("http://animethemes.test/graphql"), content);

        Assert.Equal(4, inner.Bodies.Count);
        Assert.All(inner.Bodies, body => Assert.Equal("{\"query\":\"{ __typename }\"}", body));
    }

    [Fact]
    public async Task NotFound_IsNotTriedAgain()
    {
        var inner = new CountingHandler(HttpStatusCode.NotFound);
        using var client = new HttpClient(new PollyResilienceHandler { InnerHandler = inner });

        using var response = await client.GetAsync(new Uri("http://animethemes.test/anime"));

        Assert.Equal(1, inner.Calls);
    }

    private sealed class BodyRecordingHandler : HttpMessageHandler
    {
        public System.Collections.Generic.List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return response;
        }
    }

    // Always answers with the same status; "Retry-After: 0" keeps the test fast.
    private sealed class CountingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var response = new HttpResponseMessage(status);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        }
    }
}
