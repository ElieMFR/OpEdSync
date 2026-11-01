using System;
using System.Net.Http.Headers;
using Jellyfin.Plugin.OpEdSync.Api;
using Jellyfin.Plugin.OpEdSync.Configuration;
using Jellyfin.Plugin.OpEdSync.Reports;
using Jellyfin.Plugin.OpEdSync.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.OpEdSync;

/// <inheritdoc />
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddScoped<AnimeThemesDownloader>();
        serviceCollection.AddSingleton<LibrarySelector>();
        serviceCollection.AddSingleton<RunSummaryStore>();
        serviceCollection.AddSingleton<StatsService>();
        serviceCollection.AddSingleton<StatusService>();
        serviceCollection.AddHostedService<ExoPlayerFixRegistration>();

        var productHeader = new ProductInfoHeaderValue(
            "jf-plugin-opedsync",
            applicationHost.ApplicationVersionString);

        serviceCollection.AddTransient<PollyResilienceHandler>();

        // The address is read each time a client is created, so a changed setting applies to the next run.
        serviceCollection
            .AddHttpClient("AnimeThemes", c =>
            {
                c.BaseAddress = PluginConfiguration.ResolveAnimeThemesBaseUrl(Plugin.Instance?.Configuration.AnimeThemesBaseUrl);
                c.DefaultRequestHeaders.UserAgent.Add(productHeader);
            })
            .AddHttpMessageHandler<PollyResilienceHandler>();

        // Same address, no retries: a status check must answer within seconds.
        serviceCollection.AddHttpClient(StatusService.CheckClientName, c =>
        {
            c.BaseAddress = PluginConfiguration.ResolveAnimeThemesBaseUrl(Plugin.Instance?.Configuration.AnimeThemesBaseUrl);
            c.DefaultRequestHeaders.UserAgent.Add(productHeader);
            c.Timeout = TimeSpan.FromSeconds(8);
        });
    }
}
