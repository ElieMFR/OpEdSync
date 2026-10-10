using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AnimeThemes.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemes.Tasks;

/// <summary>
/// Base class for search tasks.
/// </summary>
public abstract class BaseThemeSearchTask
{
    private const int ChunkSize = 50;
    private readonly ILogger _logger;
    private readonly AnimeThemesDownloader _downloader;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseThemeSearchTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="downloader">Downloader.</param>
    /// <param name="userManager">Use manager.</param>
    /// <param name="logger">Logger.</param>
    protected BaseThemeSearchTask(ILibraryManager libraryManager, AnimeThemesDownloader downloader, IUserManager userManager, ILogger logger)
    {
        _libraryManager = libraryManager;
        _downloader = downloader;
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
        _logger.LogInformation("Starting theme search");
        var configuration = Plugin.Instance!.Configuration;
        var applicable = GetApplicableItems(configuration);
        if (applicable.Count == 0)
        {
            _logger.LogInformation("No applicable items found -- aborting");
            return;
        }

        // @formatter:off
        var items = _libraryManager.GetItemList(
            new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Series, BaseItemKind.Movie],
                IsVirtualItem = false,
                Recursive = true,
            },
            applicable);
        // @formatter:on

        // Get the anime objects in chunks
        var itemsWithAnime = await items
            .Where(it => _downloader.ShouldUpdate(it, configuration))
            .Chunk(ChunkSize)
            .ToAsyncEnumerable()
            .SelectMany((chunk) => _downloader.ResolveItems(chunk, configuration, cancellationToken))
            .ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var semaphore = new SemaphoreSlim(1, 1);
        int counter = 0;
        int count = Math.Max(itemsWithAnime.Count, 1);

        // Process in parallel
        await Parallel.ForEachAsync(itemsWithAnime, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = configuration.DegreeOfParallelism }, async (item, ct) =>
        {
            try
            {
                await _downloader.HandleAsync(item.Item, item.Anime.First(), configuration, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // One broken item must not stop the others.
                _logger.LogError(e, "[{Id}] Failed to process {Name}", item.Item.Id, item.Item.Name);
            }

            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                counter++;
                progress.Report(counter / (double)count * 100);
            }
            finally
            {
                semaphore.Release();
            }
        }).ConfigureAwait(false);

        _logger.LogInformation("Ending theme search ({Count} matched items)", itemsWithAnime.Count);
    }

    private Regex? TryCreateRegex(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException e)
        {
            _logger.LogError(e, "Ignoring invalid library regex: {Pattern}", pattern);
            return null;
        }
    }

    private List<BaseItem> GetApplicableItems(PluginConfiguration configuration)
    {
        var includeRegex = TryCreateRegex(configuration.IncludeLibraries) ?? new Regex("^.*$", RegexOptions.IgnoreCase);
        var excludeRegex = TryCreateRegex(configuration.ExcludeLibraries);

        var libraries = _libraryManager.GetItemList(new InternalItemsQuery { IncludeItemTypes = [BaseItemKind.CollectionFolder] });

        _logger.LogInformation("Considering the following libraries: {Libraries}", libraries.Select(it => it.Name));

        var matches = libraries.Where(item => includeRegex.IsMatch(item.Name)).ToList();

        _logger.LogInformation("Matching libraries (Included): {Libraries}", matches.Select(i => i.Name));

        if (excludeRegex != null)
        {
            matches = matches.Where(item => !excludeRegex.IsMatch(item.Name)).ToList();
            _logger.LogInformation("Matching libraries (Without Excluded): {Libraries}", matches.Select(i => i.Name));
        }
        else
        {
            _logger.LogInformation("No exclusion defined -- proceeding");
        }

        return matches;
    }
}
