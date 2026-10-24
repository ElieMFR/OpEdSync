using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.OpEdSync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly IServerConfigurationManager _serverConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="serverConfiguration">Server configuration, for the display language.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IServerConfigurationManager serverConfiguration)
        : base(applicationPaths, xmlSerializer)
    {
        _serverConfiguration = serverConfiguration;
        Instance = this;
    }

    /// <inheritdoc />
    public override string Description => Localization.Pick(
        _serverConfiguration.Configuration.UICulture,
        "Télécharge automatiquement les génériques d'animes (openings et endings) depuis animethemes.moe.",
        "Automatically downloads anime openings and endings from animethemes.moe.");

    /// <inheritdoc />
    public override string Name => "OpEdSync";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("c99b0ba5-9cbf-4ee0-b45f-40873c6b5b0b");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = this.Name,
                DisplayName = this.Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace),

                // Entry in the dashboard menu, under the plugins, like NotifySync and Media Carousel.
                EnableInMainMenu = true,
                MenuIcon = "library_music",
            }
        ];
    }
}
