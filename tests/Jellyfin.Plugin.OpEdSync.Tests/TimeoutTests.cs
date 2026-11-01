using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// Audit 2026-11-01: an HTTP timeout surfaces as TaskCanceledException and was treated as the user cancelling,
// which stopped the whole run instead of failing one item.
public class TimeoutTests
{
    private static async Task<Exception> TimeoutFrom(CancellationToken cancellationToken)
    {
        using var client = new HttpClient(new HangingHandler()) { Timeout = TimeSpan.FromMilliseconds(50) };
        return await Assert.ThrowsAnyAsync<Exception>(() => client.GetAsync(new Uri("http://animethemes.test/"), cancellationToken));
    }

    [Fact]
    public async Task HttpTimeout_IsAnOperationCanceledException()
    {
        Assert.IsAssignableFrom<OperationCanceledException>(await TimeoutFrom(CancellationToken.None));
    }

    [Fact]
    public async Task HttpTimeout_WithoutCancellation_IsATimeout()
    {
        Assert.True(ThemeFileRules.IsTimeout(await TimeoutFrom(CancellationToken.None), CancellationToken.None));
    }

    [Fact]
    public async Task UserCancellation_IsNotATimeout()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(1000, cts.Token));

        Assert.False(ThemeFileRules.IsTimeout(exception, cts.Token));
    }

    [Fact]
    public void OtherErrors_AreNotTimeouts()
    {
        Assert.False(ThemeFileRules.IsTimeout(new HttpRequestException("404"), CancellationToken.None));
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }
}
