using Argus.Domain.Aggregates;
using Argus.Domain.Models;
using Argus.RiskEngine.Caches;
using Marten;
using Microsoft.Extensions.Logging;

namespace Argus.RiskEngine.Services;

/// <summary>
/// Orchestrates trade processing: loads position stream, determines event,
/// appends to Marten event store, and saves in a single transaction.
/// After persistence, updates the PositionCache for real-time risk calculations.
/// Scoped lifetime — one instance per trade via IServiceScopeFactory.
/// </summary>
public sealed class TradeProcessor
{
    private readonly IDocumentSession _session;
    private readonly PositionCache _positionCache;
    private readonly ILogger<TradeProcessor> _logger;

    public TradeProcessor(
        IDocumentSession session,
        PositionCache positionCache,
        ILogger<TradeProcessor> logger)
    {
        _session = session;
        _positionCache = positionCache;
        _logger = logger;
    }

    public async Task ProcessAsync(Trade trade, CancellationToken cancellationToken = default)
    {
        // Load current position state (null if no events yet for this instrument)
        var position = await _session.Events.AggregateStreamAsync<Position>(
            trade.InstrumentId, token: cancellationToken);

        // Determine which event to emit
        var @event = TradeEventDeterminer.Determine(position, trade);

        // Append event to the instrument's stream
        _session.Events.Append(trade.InstrumentId, @event);

        // Save — persists event + updates inline Position projection in one transaction
        await _session.SaveChangesAsync(cancellationToken);

        // Reload updated position from Marten and sync to cache
        var updatedPosition = await _session.Events.AggregateStreamAsync<Position>(
            trade.InstrumentId, token: cancellationToken);

        if (updatedPosition != null)
            _positionCache.Update(updatedPosition);

        _logger.LogInformation(
            "{EventType} for {Symbol}: qty={Qty}, price={Price:F2}",
            @event.GetType().Name,
            trade.Symbol,
            trade.Quantity,
            trade.Price);
    }
}
