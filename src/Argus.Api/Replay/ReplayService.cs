using Argus.Domain.Models;
using Marten;

namespace Argus.Api.Replay;

/// <summary>
/// Service for querying historical RiskSnapshots for replay.
/// Scoped lifetime — uses IQuerySession for read-only database access.
/// </summary>
public sealed class ReplayService
{
    private readonly IQuerySession _session;

    public ReplayService(IQuerySession session)
    {
        _session = session;
    }

    /// <summary>
    /// Get all snapshots within a time range, ordered by timestamp.
    /// </summary>
    public async Task<IReadOnlyList<RiskSnapshot>> GetSnapshotsInRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        return await _session.Query<RiskSnapshot>()
            .Where(s => s.Timestamp >= from && s.Timestamp <= to)
            .OrderBy(s => s.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get the next snapshot after the specified time within the end boundary.
    /// Returns null if no more snapshots exist.
    /// </summary>
    public async Task<RiskSnapshot?> GetNextSnapshotAsync(
        DateTimeOffset afterTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        return await _session.Query<RiskSnapshot>()
            .Where(s => s.Timestamp > afterTime && s.Timestamp <= endTime)
            .OrderBy(s => s.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Get the count of snapshots available in a time range.
    /// Useful for UI progress indicators.
    /// </summary>
    public async Task<int> GetSnapshotCountAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        return await _session.Query<RiskSnapshot>()
            .Where(s => s.Timestamp >= from && s.Timestamp <= to)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Get the earliest and latest snapshot timestamps in the database.
    /// Returns null if no snapshots exist.
    /// </summary>
    public async Task<(DateTimeOffset Earliest, DateTimeOffset Latest)?> GetAvailableRangeAsync(
        CancellationToken cancellationToken = default)
    {
        var earliest = await _session.Query<RiskSnapshot>()
            .OrderBy(s => s.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (earliest is null)
            return null;

        var latest = await _session.Query<RiskSnapshot>()
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        return (earliest.Timestamp, latest!.Timestamp);
    }
}
