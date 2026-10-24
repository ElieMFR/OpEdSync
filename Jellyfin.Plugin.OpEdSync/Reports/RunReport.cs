using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Jellyfin.Plugin.OpEdSync.Reports;

/// <summary>
/// Collects what happens during one run. Thread safe: items are processed in parallel.
/// </summary>
public sealed class RunReport
{
    private readonly ConcurrentDictionary<Guid, MissingReason> _reasons = new();
    private int _added;
    private int _removed;
    private int _failed;

    /// <summary>
    /// Gets the start of the run.
    /// </summary>
    public DateTime StartedUtc { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets how many items were candidates (without theme, or all with forced sync).
    /// </summary>
    public int Candidates { get; set; }

    /// <summary>
    /// Gets or sets how many items were matched with an anime.
    /// </summary>
    public int Matched { get; set; }

    /// <summary>
    /// Gets the number of theme files written.
    /// </summary>
    public int Added => _added;

    /// <summary>
    /// Gets the number of theme files removed.
    /// </summary>
    public int Removed => _removed;

    /// <summary>
    /// Gets the number of failed downloads.
    /// </summary>
    public int Failed => _failed;

    /// <summary>
    /// Records a reason for an item, keeping the most informative one.
    /// </summary>
    /// <param name="itemId">Item ID.</param>
    /// <param name="reason">Reason.</param>
    public void SetReason(Guid itemId, MissingReason reason)
        => _reasons.AddOrUpdate(itemId, reason, (_, current) => reason > current ? reason : current);

    /// <summary>
    /// Forgets the reason of an item that finally got a theme.
    /// </summary>
    /// <param name="itemId">Item ID.</param>
    public void ClearReason(Guid itemId) => _reasons.TryRemove(itemId, out _);

    /// <summary>
    /// Counts a written theme file.
    /// </summary>
    public void CountAdded() => Interlocked.Increment(ref _added);

    /// <summary>
    /// Counts removed theme files.
    /// </summary>
    /// <param name="count">Number of files.</param>
    public void CountRemoved(int count) => Interlocked.Add(ref _removed, count);

    /// <summary>
    /// Counts a failed download.
    /// </summary>
    public void CountFailed() => Interlocked.Increment(ref _failed);

    /// <summary>
    /// Builds the summary saved at the end of the run.
    /// </summary>
    /// <param name="cancelled">Whether the run was cancelled.</param>
    /// <returns>The summary.</returns>
    public RunSummary ToSummary(bool cancelled) => new()
    {
        StartedUtc = StartedUtc,
        EndedUtc = DateTime.UtcNow,
        Cancelled = cancelled,
        Candidates = Candidates,
        Matched = Matched,
        Added = Added,
        Removed = Removed,
        Failed = Failed,
        Reasons = _reasons.ToDictionary(it => it.Key, it => it.Value),
    };
}
