using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.OpEdSync.Reports;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// Computes the figures of the configuration page and keeps them for a few minutes: going through every theme of
/// the selected libraries takes about a second on a real library, too slow to redo on every page view.
/// The cache is dropped when a run ends or the settings change.
/// </summary>
public sealed class StatsService
{
    // The list of items without theme is for display only; a few hundred is plenty.
    private const int MaxMissingListed = 500;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly LibrarySelector _librarySelector;
    private readonly ILibraryManager _libraryManager;
    private readonly RunSummaryStore _summaryStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private StatsResponse? _cached;
    private bool _subscribed;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatsService"/> class.
    /// </summary>
    /// <param name="librarySelector">Library selector.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="summaryStore">Store of the last run summary.</param>
    /// <param name="scopeFactory">Scope factory, for the scoped downloader.</param>
    public StatsService(LibrarySelector librarySelector, ILibraryManager libraryManager, RunSummaryStore summaryStore, IServiceScopeFactory scopeFactory)
    {
        _librarySelector = librarySelector;
        _libraryManager = libraryManager;
        _summaryStore = summaryStore;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Drops the cached figures.
    /// </summary>
    public void Invalidate()
    {
        lock (_lock)
        {
            _cached = null;
        }
    }

    /// <summary>
    /// Gets the figures, from the cache when they are recent enough.
    /// </summary>
    /// <param name="refresh">Whether to ignore the cache.</param>
    /// <returns>The statistics.</returns>
    public StatsResponse Get(bool refresh = false)
    {
        SubscribeToConfigurationChanges();

        lock (_lock)
        {
            if (!refresh && _cached is not null && DateTime.UtcNow - _cached.ComputedUtc < CacheDuration)
            {
                return _cached;
            }
        }

        var computed = Compute();
        lock (_lock)
        {
            _cached = computed;
        }

        return computed;
    }

    private void SubscribeToConfigurationChanges()
    {
        if (_subscribed || Plugin.Instance is not { } plugin)
        {
            return;
        }

        _subscribed = true;
        plugin.ConfigurationChanged += OnConfigurationChanged;
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e) => Invalidate();

    private StatsResponse Compute()
    {
        var configuration = Plugin.Instance!.Configuration;
        var summary = _summaryStore.Load();

        var libraries = _librarySelector.GetAllLibraries()
            .Select(it => new LibraryInfo
            {
                Id = it.Id,
                Name = it.Name,
                Included = _librarySelector.IsIncluded(it, configuration),
                ItemCount = CountItems(it),
            })
            .OrderBy(it => it.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var response = new StatsResponse { Libraries = libraries, ComputedUtc = DateTime.UtcNow };
        var reasons = Enum.GetValues<MissingReason>().ToDictionary(it => it.ToString(), _ => 0);
        var missing = new List<MissingItem>();

        using var scope = _scopeFactory.CreateScope();
        var downloader = scope.ServiceProvider.GetRequiredService<AnimeThemesDownloader>();

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

            var reason = !downloader.HasAnyId(item, configuration)
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

    private int CountItems(BaseItem library)
        => _libraryManager.GetItemList(
            new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Series, BaseItemKind.Movie],
                IsVirtualItem = false,
                Recursive = true,
            },
            [library]).Count;
}
