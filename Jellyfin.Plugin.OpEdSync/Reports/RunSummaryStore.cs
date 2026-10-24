using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OpEdSync.Reports;

/// <summary>
/// Reads and writes the summary of the last run (<c>last-run.json</c> in the plugin data folder).
/// </summary>
public sealed class RunSummaryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ILogger<RunSummaryStore> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RunSummaryStore"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public RunSummaryStore(ILogger<RunSummaryStore> logger)
    {
        _logger = logger;
    }

    private static string? FilePath => Plugin.Instance is { } plugin
        ? Path.Combine(plugin.DataFolderPath, "last-run.json")
        : null;

    /// <summary>
    /// Saves a summary, replacing the previous one.
    /// </summary>
    /// <param name="summary">Summary.</param>
    public void Save(RunSummary summary)
    {
        var path = FilePath;
        if (path is null)
        {
            return;
        }

        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(summary, JsonOptions));
                File.Move(temp, path, true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(e, "OpEdSync: could not save the run summary");
        }
    }

    /// <summary>
    /// Loads the last summary.
    /// </summary>
    /// <returns>The summary, or null when there is none or it can't be read.</returns>
    public RunSummary? Load()
    {
        var path = FilePath;
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            lock (_lock)
            {
                return JsonSerializer.Deserialize<RunSummary>(File.ReadAllText(path), JsonOptions);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(e, "OpEdSync: last run summary is unreadable, ignored");
            return null;
        }
    }
}
