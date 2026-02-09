using System.Collections.Concurrent;
using Argus.Domain.Aggregates;
using Marten;

namespace Argus.RiskEngine.Caches;

/// <summary>
/// Thread-safe in-memory store for open positions.
/// Hydrated from Marten at startup, then kept in sync by TradeProcessor.
/// Read by RiskSnapshotWorker for risk calculations.
/// Registered as a singleton.
/// </summary>
public sealed class PositionCache
{
    private readonly ConcurrentDictionary<Guid, Position> _positions = new();

    /// <summary>
    /// Hydrate cache from Marten on startup — loads all open positions.
    /// </summary>
    public async Task LoadFromStoreAsync(IDocumentSession session, CancellationToken cancellationToken = default)
    {
        var openPositions = await session.Query<Position>()
            .Where(p => p.IsOpen)
            .ToListAsync(cancellationToken);

        foreach (var position in openPositions)
        {
            _positions[position.InstrumentId] = position;
        }
    }

    public void Update(Position position)
    {
        if (position.IsOpen)
            _positions[position.InstrumentId] = position;
        else
            _positions.TryRemove(position.InstrumentId, out _);
    }

    public void Remove(Guid instrumentId) => _positions.TryRemove(instrumentId, out _);

    public IReadOnlyList<Position> GetAll() => _positions.Values.ToList();

    public int Count => _positions.Count;
}
