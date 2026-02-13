using Argus.Domain.Aggregates;
using Argus.Domain.Models;
using Argus.Domain.Services;
using Marten;

namespace Argus.Api.Services;

/// <summary>
/// Orchestrates on-demand reconciliation:
/// 1. Replays all event streams through fresh Position aggregates
/// 2. Loads current live positions (inline projections)
/// 3. Checksums both sets and compares field-by-field
///
/// Scoped lifetime — uses IDocumentSession (one per request).
/// </summary>
public sealed class ReconciliationService
{
    private readonly IDocumentSession _session;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(IDocumentSession session, ILogger<ReconciliationService> logger)
    {
        _session = session;
        _logger = logger;
    }

    public async Task<ReconciliationReport> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reconciliation started");

        // Step 1: Load all live positions (inline projection — this is the "actual" state)
        var livePositions = await _session.Query<Position>().ToListAsync(cancellationToken);

        // Step 2: Get all event stream IDs that have Position events
        // We query all Positions (including closed ones) to get the full set of stream IDs
        var streamIds = livePositions.Select(p => p.InstrumentId).ToList();

        // Step 3: Replay each stream from events (fresh aggregate = "expected" state)
        var replayedPositions = new List<Position>();
        var totalEvents = 0;

        foreach (var streamId in streamIds)
        {
            var replayed = await _session.Events.AggregateStreamAsync<Position>(
                streamId, token: cancellationToken);

            if (replayed is not null)
                replayedPositions.Add(replayed);

            // Count events in this stream
            var events = await _session.Events.FetchStreamAsync(streamId, token: cancellationToken);
            totalEvents += events.Count;
        }

        // Step 4: Compute checksums
        var expectedChecksum = PortfolioChecksumCalculator.Compute(replayedPositions);
        var actualChecksum = PortfolioChecksumCalculator.Compute(livePositions);

        // Step 5: Compare field-by-field
        var discrepancies = PositionComparer.Compare(replayedPositions, livePositions);

        var passed = expectedChecksum == actualChecksum && discrepancies.Count == 0;

        var report = new ReconciliationReport(
            Timestamp: DateTimeOffset.UtcNow,
            TotalEventsReplayed: totalEvents,
            ExpectedChecksum: expectedChecksum,
            ActualChecksum: actualChecksum,
            Passed: passed,
            Discrepancies: discrepancies);

        _logger.LogInformation(
            "Reconciliation {Status}: {EventCount} events, {PositionCount} positions, {DiscrepancyCount} discrepancies",
            passed ? "PASSED" : "FAILED",
            totalEvents,
            replayedPositions.Count,
            discrepancies.Count);

        return report;
    }
}
