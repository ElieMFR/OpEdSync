using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.OpEdSync.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// Decides which libraries the plugin works on, shared by the scheduled task and the statistics.
/// </summary>
public sealed class LibrarySelector
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<LibrarySelector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibrarySelector"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="logger">Logger.</param>
    public LibrarySelector(ILibraryManager libraryManager, ILogger<LibrarySelector> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets all libraries of the server.
    /// </summary>
    /// <returns>The libraries.</returns>
    public IReadOnlyList<BaseItem> GetAllLibraries()
        => _libraryManager.GetItemList(new InternalItemsQuery { IncludeItemTypes = [BaseItemKind.CollectionFolder] });

    /// <summary>
    /// Tells whether a library is selected by the configuration.
    /// </summary>
    /// <param name="library">Library.</param>
    /// <param name="configuration">Configuration.</param>
    /// <returns>Whether the plugin works on it.</returns>
    public bool IsIncluded(BaseItem library, PluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(configuration);

        // Settings saved by the new page: an explicit list of library IDs.
        if (configuration.UseLibraryList)
        {
            var id = library.Id.ToString("N");
            return configuration.LibraryIds.Any(it => string.Equals(it.Replace("-", string.Empty, StringComparison.Ordinal), id, StringComparison.OrdinalIgnoreCase));
        }

        // Older settings: regular expressions on library names.
        var include = TryCreateRegex(configuration.IncludeLibraries) ?? new Regex("^.*$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        var exclude = TryCreateRegex(configuration.ExcludeLibraries);
        return include.IsMatch(library.Name) && (exclude is null || !exclude.IsMatch(library.Name));
    }

    /// <summary>
    /// Gets the libraries selected by the configuration.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <returns>The selected libraries.</returns>
    private List<BaseItem> GetIncludedLibraries(PluginConfiguration configuration)
    {
        var libraries = GetAllLibraries();
        var included = libraries.Where(it => IsIncluded(it, configuration)).ToList();
        _logger.LogInformation("OpEdSync: selected libraries: {Libraries}", included.Select(it => it.Name));
        return included;
    }

    /// <summary>
    /// Gets the series and movies of the selected libraries.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <returns>The items, empty when no library is selected.</returns>
    public IReadOnlyList<BaseItem> GetItems(PluginConfiguration configuration)
    {
        var libraries = GetIncludedLibraries(configuration);
        if (libraries.Count == 0)
        {
            return [];
        }

        return _libraryManager.GetItemList(
            new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Series, BaseItemKind.Movie],
                IsVirtualItem = false,
                Recursive = true,
            },
            libraries);
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
            _logger.LogError(e, "OpEdSync: ignoring invalid library pattern: {Pattern}", pattern);
            return null;
        }
    }
}
