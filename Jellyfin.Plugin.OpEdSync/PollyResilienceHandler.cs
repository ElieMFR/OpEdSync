using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// Resilience handler that uses Polly under the hood.
/// </summary>
public sealed class PollyResilienceHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollyResilienceHandler"/> class.
    /// </summary>
    public PollyResilienceHandler()
    {
        _pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(GetRetryOptions())
            .Build();
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return _pipeline.ExecuteAsync(
            async token => await base.SendAsync(request, token).ConfigureAwait(false),
            cancellationToken).AsTask();
    }

    private static ValueTask<bool> HandleTransientHttpError(Outcome<HttpResponseMessage> outcome) => outcome switch
    {
        { Exception: HttpRequestException } => PredicateResult.True(),
        { Result.StatusCode: HttpStatusCode.RequestTimeout } => PredicateResult.True(),
        { Result.StatusCode: HttpStatusCode.TooManyRequests } => PredicateResult.True(),
        { Result.StatusCode: >= HttpStatusCode.InternalServerError } => PredicateResult.True(),
        _ => PredicateResult.False()
    };

    private static RetryStrategyOptions<HttpResponseMessage> GetRetryOptions() => new()
    {
        ShouldHandle = args => HandleTransientHttpError(args.Outcome),
        OnRetry = args =>
        {
            // The answer that is given up on is never read: release its connection before the next attempt.
            args.Outcome.Result?.Dispose();
            return default;
        },
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(2),
        DelayGenerator = args => ValueTask.FromResult(
            RetryAfterDelay(args.Outcome.Result?.Headers.RetryAfter, DateTimeOffset.UtcNow))
    };

    /// <summary>
    /// Reads how long the server asked to wait. <c>Retry-After</c> is either a number of seconds or an HTTP date;
    /// without it, the exponential backoff applies.
    /// </summary>
    /// <param name="retryAfter">The header, if any.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The wait, never negative, or <c>null</c> to use the backoff.</returns>
    public static TimeSpan? RetryAfterDelay(RetryConditionHeaderValue? retryAfter, DateTimeOffset now)
    {
        TimeSpan? wait = retryAfter?.Delta is { } delta
            ? delta
            : retryAfter?.Date is { } date
                ? (date > now ? date - now : TimeSpan.Zero)
                : null;

        // A server (or a proxy) asking for hours would freeze the run: same ceiling as the rate limit gate.
        return wait > RateLimitGate.MaxWait ? RateLimitGate.MaxWait : wait;
    }
}
