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

    // AnimeThemes needs a User-Agent (a request without one gets a 403) but not the version of the Jellyfin server.
    [Fact]
    public void UserAgent_NamesThePlugin_WithItsOwnVersion()
    {
        var agent = PluginServiceRegistrator.CreateUserAgent();

        Assert.Equal("jf-plugin-opedsync", agent.Product!.Name);
        Assert.Equal(typeof(Plugin).Assembly.GetName().Version!.ToString(), agent.Product.Version);
    }

    // The real registration, then what goes on the wire: the header of the client the plugin really uses.
    [Fact]
    public async Task TheClientOfThePlugin_SendsThePluginUserAgent()
    {
        var agents = new List<string>();
        var services = new ServiceCollection();
        new PluginServiceRegistrator().RegisterServices(services, null!);
        services.AddHttpClient("AnimeThemes").ConfigurePrimaryHttpMessageHandler(() => new AgentHandler(agents));
        using var provider = services.BuildServiceProvider();

        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("AnimeThemes");
        await client.GetAsync(new Uri("http://127.0.0.1:1/"));

        Assert.Equal([PluginServiceRegistrator.CreateUserAgent().ToString()], agents);
        Assert.DoesNotContain("12.", agents[0], StringComparison.Ordinal);
    }

    private sealed class AgentHandler(List<string> agents) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            agents.Add(request.Headers.UserAgent.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class RecordingHandler(List<string> hosts) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            hosts.Add(request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"filterAnimeByExternalSite\":[]}}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
