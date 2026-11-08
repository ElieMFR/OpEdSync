using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// Slows the lookups down before AnimeThemes' limit of 90 requests per minute is reached, from the headers of its last
/// answer. Lookups follow one another at about 75 a minute, so a large library would otherwise hit the limit after
/// two minutes of searching.
/// </summary>
public sealed class RateLimitGate
{
    /// <summary>
    /// Number of requests left at which the gate starts waiting for the window to end.
    /// </summary>
    public const int Margin = 3;

    /// <summary>
    /// Longest wait asked of a single request, whatever the headers say.
    /// </summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(70);

    private readonly object _lock = new();
    private int? _remaining;
    private DateTimeOffset _reset = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets the gate shared by every lookup of the plugin: the limit is the server's, not a run's.
    /// </summary>
    public static RateLimitGate Shared { get; } = new();

    /// <summary>
    /// Records what the last answer said about the limit.
    /// </summary>
    /// <param name="remaining">Value of <c>x-ratelimit-remaining</c>, if any.</param>
    /// <param name="resetEpochSeconds">Value of <c>x-ratelimit-reset</c> (Unix seconds), if any.</param>
    public void Update(int? remaining, long? resetEpochSeconds)
    {
        lock (_lock)
        {
            _remaining = remaining;
            _reset = resetEpochSeconds is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : DateTimeOffset.MinValue;
        }
    }

    /// <summary>
    /// Tells how long to wait before the next request.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The wait, zero when requests are still allowed.</returns>
    public TimeSpan DelayAt(DateTimeOffset now)
    {
        lock (_lock)
        {
            if (_remaining is not { } remaining || remaining > Margin)
            {
                return TimeSpan.Zero;
            }

            var wait = _reset - now;
            if (wait <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            wait += TimeSpan.FromSeconds(1);
            return wait > MaxWait ? MaxWait : wait;
        }
    }

    /// <summary>
    /// Waits as long as <see cref="DelayAt"/> asks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that ends when the next request may go.</returns>
    public Task WaitAsync(CancellationToken cancellationToken)
    {
        var delay = DelayAt(DateTimeOffset.UtcNow);
        return delay > TimeSpan.Zero ? Task.Delay(delay, cancellationToken) : Task.CompletedTask;
    }
}
