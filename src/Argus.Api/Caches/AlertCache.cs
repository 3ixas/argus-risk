using System.Collections.Concurrent;
using Argus.Domain.Models;

namespace Argus.Api.Caches;

/// <summary>
/// Thread-safe in-memory store of active (unresolved) alerts.
/// Keyed by "{type}:{component}" — mirrors AlertPublisher's deduplication key.
/// Resolved alerts are removed on receipt; active alerts are returned sorted newest-first.
/// </summary>
public sealed class AlertCache
{
    private readonly ConcurrentDictionary<string, Alert> _active = new();

    public int ActiveCount => _active.Count;

    /// <summary>
    /// Updates the cache based on an incoming alert.
    /// Resolved alerts remove the entry; new/updated alerts add/replace it.
    /// </summary>
    public void Update(Alert alert)
    {
        var key = BuildKey(alert);

        if (alert.IsResolved)
            _active.TryRemove(key, out _);
        else
            _active[key] = alert;
    }

    /// <summary>
    /// Returns all active (unresolved) alerts, sorted newest-first.
    /// </summary>
    public IReadOnlyList<Alert> GetActive() =>
        _active.Values
            .OrderByDescending(a => a.Timestamp)
            .ToList();

    private static string BuildKey(Alert alert) => $"{alert.Type}:{alert.Component}";
}
