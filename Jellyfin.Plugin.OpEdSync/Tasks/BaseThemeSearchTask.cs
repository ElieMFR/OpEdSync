using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.OpEdSync.Reports;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OpEdSync.Tasks;

/// <summary>
/// Base class for search tasks.
/// </summary>
public abstract class BaseThemeSearchTask
{
    private const int ChunkSize = 50;

    // Share of the progress bar given to the AnimeThemes lookups; the downloads get the rest.
    private const double LookupShare = 20;

    private readonly ILogger _logger;
    private readonly AnimeThemesDownloader _downloader;
    private readonly LibrarySelector _librarySelector;
    private readonly RunSummaryStore _summaryStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseThemeSearchTask"/> class.
    /// </summary>
    /// <param name="librarySelector">Library selector.</param>
    /// <param name="downloader">Downloader.</param>
    /// <param name="summaryStore">Store for the summary of the run.</param>
    /// <param name="logger">Logger.</param>
    protected BaseThemeSearchTask(LibrarySelector librarySelector, AnimeThemesDownloader downloader, RunSummaryStore summaryStore, ILogger logger)
    {
        _librarySelector = librarySelector;
        _downloader = downloader;
        _summaryStore = summaryStore;
        _logger = logger;
    }

    /// <summary>
    /// Processes all relevant items and calls a callback function.
    /// </summary>
    /// <param name="progress">Progress indicator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that runs until the process is done.</returns>
    protected async Task FindAndProcessAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        _logger.LogInformation("Starting theme search");
        var configuration = Plugin.Instance!.Configuration;
        var report = new RunReport();
        bool cancelled = true;

        try
        {
            var items = _librarySelector.GetItems(configuration);
            if (items.Count == 0)
            {
                _logger.LogInformation("No applicable items found -- aborting");
                cancelled = false;
                return;
            }

            var candidates = items.Where(it => _downloader.ShouldUpdate(it, configuration)).ToList();
            report.Candidates = candidates.Count;

            // Look the anime up in chunks; report progress so the bar does not sit at 0 % meanwhile.
            var chunks = candidates.Chunk(ChunkSize).ToList();
            var itemsWithAnime = new System.Collections.Generic.List<Models.ItemWithAnime>();
            for (int i = 0; i < chunks.Count; i++)
            {
                await foreach (var resolved in _downloader.ResolveItems(chunks[i], configuration, report, cancellationToken).ConfigureAwait(false))
                {
                    itemsWithAnime.Add(resolved);
                }

                progress.Report((i + 1) / (double)chunks.Count * LookupShare);
            }

            report.Matched = itemsWithAnime.Count;

            int counter = 0;
            int count = Math.Max(itemsWithAnime.Count, 1);
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,

                // 0 would make ParallelOptions throw on every run.
                MaxDegreeOfParallelism = ThemeFileRules.ClampParallelism(configuration.DegreeOfParallelism),
            };

            await Parallel.ForEachAsync(itemsWithAnime, options, async (item, ct) =>
            {
                try
                {
                    await _downloader.HandleAsync(item.Item, item.Anime.First(), configuration, report, ct).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // One broken item must not stop the others.
                    _logger.LogError(e, "[{Id}] Failed to process {Name}", item.Item.Id, item.Item.Name);
                    report.SetReason(item.Item.Id, MissingReason.DownloadFailed);
                }

                var done = Interlocked.Increment(ref counter);
                progress.Report(LookupShare + (done / (double)count * (100 - LookupShare)));
            }).ConfigureAwait(false);

            cancelled = false;
            _logger.LogInformation(
                "Ending theme search: {Matched} matched, {Added} added, {Removed} removed, {Failed} failed",
                report.Matched,
                report.Added,
                report.Removed,
                report.Failed);
        }
        finally
        {
            _summaryStore.Save(report.ToSummary(cancelled));
        }
    }
}
