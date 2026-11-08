using System;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// AnimeThemes allows 90 requests a minute and tells, in the headers of every answer, how many are left and when the
// window ends. The lookups follow one another at about 75 a minute, so a large library reaches the limit in two minutes.
public class RateLimitGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BeforeAnyAnswer_NothingWaits()
    {
        Assert.Equal(TimeSpan.Zero, new RateLimitGate().DelayAt(Now));
    }

    [Theory]
    [InlineData(89)]
    [InlineData(50)]
    [InlineData(4)]
    public void WithRequestsToSpare_NothingWaits(int remaining)
    {
        var gate = new RateLimitGate();
        gate.Update(remaining, Now.AddSeconds(30).ToUnixTimeSeconds());

        Assert.Equal(TimeSpan.Zero, gate.DelayAt(Now));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void NearTheLimit_WaitsForTheEndOfTheWindow(int remaining)
    {
        var gate = new RateLimitGate();
        gate.Update(remaining, Now.AddSeconds(30).ToUnixTimeSeconds());

        Assert.Equal(TimeSpan.FromSeconds(31), gate.DelayAt(Now));
        Assert.Equal(TimeSpan.FromSeconds(6), gate.DelayAt(Now.AddSeconds(25)));
    }

    [Fact]
    public void OnceTheWindowEnded_NothingWaits()
    {
        var gate = new RateLimitGate();
        gate.Update(0, Now.AddSeconds(30).ToUnixTimeSeconds());

        Assert.Equal(TimeSpan.Zero, gate.DelayAt(Now.AddSeconds(30)));
        Assert.Equal(TimeSpan.Zero, gate.DelayAt(Now.AddMinutes(5)));
    }

    [Fact]
    public void ADistantReset_NeverMakesTheRunWaitMoreThanTheCap()
    {
        var gate = new RateLimitGate();
        gate.Update(0, Now.AddHours(1).ToUnixTimeSeconds());

        Assert.Equal(RateLimitGate.MaxWait, gate.DelayAt(Now));
    }

    [Fact]
    public void AnAnswerWithoutHeaders_ForgetsTheLimit()
    {
        var gate = new RateLimitGate();
        gate.Update(0, Now.AddSeconds(30).ToUnixTimeSeconds());
        gate.Update(null, null);

        Assert.Equal(TimeSpan.Zero, gate.DelayAt(Now));
    }
}
