using System;
using System.Net.Http.Headers;
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
        serviceCollection.AddHostedService<ExoPlayerFixRegistration>();

        var productHeader = new ProductInfoHeaderValue(
            "jf-plugin-opedsync",
            applicationHost.ApplicationVersionString);

        serviceCollection.AddTransient<PollyResilienceHandler>();

        serviceCollection
            .AddHttpClient("AnimeThemes", c =>
            {
                c.BaseAddress = new Uri("https://api.animethemes.moe");
                c.DefaultRequestHeaders.UserAgent.Add(productHeader);
            })
            .AddHttpMessageHandler<PollyResilienceHandler>();
    }
}
