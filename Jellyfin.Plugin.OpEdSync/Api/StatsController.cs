using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.OpEdSync.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// Figures shown at the top of the configuration page.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("OpEdSync")]
[Produces("application/json")]
public class StatsController : ControllerBase
{
    // The list of items without theme is for display only; a few hundred is plenty.
    private const int MaxMissingListed = 500;

    private readonly LibrarySelector _librarySelector;
    private readonly AnimeThemesDownloader _downloader;
    private readonly RunSummaryStore _summaryStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatsController"/> class.
    /// </summary>
    /// <param name="librarySelector">Library selector.</param>
    /// <param name="downloader">Downloader, used to know whether an item has a usable ID.</param>
    /// <param name="summaryStore">Store of the last run summary.</param>
    public StatsController(LibrarySelector librarySelector, AnimeThemesDownloader downloader, RunSummaryStore summaryStore)
    {
        _librarySelector = librarySelector;
        _downloader = downloader;
        _summaryStore = summaryStore;
    }

    /// <summary>
    /// Gets the coverage of the selected libraries and the summary of the last run.
    /// </summary>
    /// <returns>The statistics.</returns>
    [HttpGet("Stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<StatsResponse> GetStats()
    {
        var configuration = Plugin.Instance!.Configuration;
        var summary = _summaryStore.Load();

        var libraries = _librarySelector.GetAllLibraries()
            .Select(it => new LibraryInfo { Id = it.Id, Name = it.Name, Included = _librarySelector.IsIncluded(it, configuration) })
            .OrderBy(it => it.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var response = new StatsResponse { Libraries = libraries };
        var reasons = Enum.GetValues<MissingReason>().ToDictionary(it => it.ToString(), _ => 0);
        var missing = new List<MissingItem>();

        foreach (var item in _librarySelector.GetItems(configuration))
        {
            response.Total++;
            var songs = item.GetThemeSongs();
            if (songs.Count > 0 || item.GetThemeVideos().Count > 0)
            {
                response.WithTheme++;
                if (songs.Any(it => it.Path is not null && ThemeFileRules.IsPluginFile(it.Path)))
                {
                    response.FromPlugin++;
                }

                continue;
            }

            var reason = !_downloader.HasAnyId(item, configuration)
                ? MissingReason.NoId
                : summary?.Reasons.GetValueOrDefault(item.Id, MissingReason.NotAnalysed) ?? MissingReason.NotAnalysed;

            reasons[reason.ToString()]++;
            if (missing.Count < MaxMissingListed)
            {
                missing.Add(new MissingItem { Id = item.Id, Name = item.Name, Year = item.ProductionYear, Reason = reason.ToString() });
            }
        }

        response.Reasons = reasons;
        response.Missing = missing.OrderBy(it => it.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        response.LastRun = summary is null ? null : new LastRunInfo
        {
            StartedUtc = summary.StartedUtc,
            EndedUtc = summary.EndedUtc,
            Cancelled = summary.Cancelled,
            Candidates = summary.Candidates,
            Matched = summary.Matched,
            Added = summary.Added,
            Removed = summary.Removed,
            Failed = summary.Failed,
        };

        return response;
    }
}
