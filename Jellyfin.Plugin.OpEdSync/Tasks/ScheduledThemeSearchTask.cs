using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.OpEdSync.Reports;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OpEdSync.Tasks;

/// <summary>
/// Scheduled task for enforcing configuration.
/// </summary>
public class ScheduledThemeSearchTask : BaseThemeSearchTask, IScheduledTask
{
    private readonly ILogger<ScheduledThemeSearchTask> _logger;
    private readonly IServerConfigurationManager _serverConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledThemeSearchTask"/> class.
    /// </summary>
    /// <param name="downloader">Downloader.</param>
    /// <param name="librarySelector">Library selector.</param>
    /// <param name="summaryStore">Store for the summary of the run.</param>
    /// <param name="serverConfiguration">Server configuration, for the display language.</param>
    /// <param name="logger">Logger.</param>
    public ScheduledThemeSearchTask(AnimeThemesDownloader downloader, LibrarySelector librarySelector, RunSummaryStore summaryStore, IServerConfigurationManager serverConfiguration, ILogger<ScheduledThemeSearchTask> logger) : base(librarySelector, downloader, summaryStore, logger)
    {
        _logger = logger;
        _serverConfiguration = serverConfiguration;
    }

    /// <inheritdoc />
    public string Name => Text("Rechercher les thèmes d'anime", "Search for anime themes");

    /// <inheritdoc />
    public string Key { get; } = "opedsync.search";

    /// <inheritdoc />
    public string Description => Text(
        "Télécharge les génériques d'animes manquants depuis AnimeThemes, selon les réglages d'OpEdSync.",
        "Downloads missing anime openings and endings from AnimeThemes, following the OpEdSync settings.");

    /// <inheritdoc />
    public string Category => Text("Thèmes musicaux", "Theme songs");

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting search for anime themes");
        return FindAndProcessAsync(progress, cancellationToken);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(12).Ticks };
    }

    private string Text(string french, string english)
        => Localization.Pick(_serverConfiguration.Configuration.UICulture, french, english);
}
