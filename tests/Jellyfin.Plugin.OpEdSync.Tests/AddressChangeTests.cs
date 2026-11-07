using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.OpEdSync.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// Found on the kit's bench (2026-11-01): the scheduled task is built once at startup, so a client created in the
// constructor kept the old address until the server restarted, and the run went to the public API.
public class AddressChangeTests
{
    [Fact]
    public async Task ChangedAddress_IsUsedByTheNextLookup_WithoutRestart()
    {
        var address = "http://first.test";
        var hosts = new List<string>();

        var services = new ServiceCollection();
        services.AddHttpClient("AnimeThemes", c => c.BaseAddress = new Uri(address))
            .ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(hosts));
        using var provider = services.BuildServiceProvider();

        var api = new AnimeThemesApi(provider.GetRequiredService<IHttpClientFactory>(), NullLogger<AnimeThemesDownloader>.Instance);

        await api.FindByExternalId(Sites.ANIDB, [1], CancellationToken.None);
        address = "http://second.test";
        await api.FindByExternalId(Sites.ANIDB, [1], CancellationToken.None);

        Assert.Equal(["first.test", "second.test"], hosts);
    }

    private sealed class RecordingHandler(List<string> hosts) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            hosts.Add(request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"anime\":[],\"links\":{\"next\":null}}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
