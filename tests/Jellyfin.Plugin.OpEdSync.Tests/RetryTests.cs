using System;
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

    [Fact]
    public async Task NotFound_IsNotTriedAgain()
    {
        var inner = new CountingHandler(HttpStatusCode.NotFound);
        using var client = new HttpClient(new PollyResilienceHandler { InnerHandler = inner });

        using var response = await client.GetAsync(new Uri("http://animethemes.test/anime"));

        Assert.Equal(1, inner.Calls);
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
