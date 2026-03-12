using System.Collections.Concurrent;
using Argus.Domain.Enums;
using Argus.Domain.Models;

namespace Argus.RiskEngine.Caches;

/// <summary>
/// Thread-safe in-memory store for latest market prices, FX rates, and price history.
/// Updated by PriceConsumerWorker and FxRateConsumerWorker.
/// Read by RiskSnapshotWorker for risk calculations.
/// Registered as a singleton.
/// </summary>
public sealed class MarketDataCache
{
    private readonly ConcurrentDictionary<Guid, PriceTick> _prices = new();
    private readonly ConcurrentDictionary<(Currency Base, Currency Quote), FxRate> _fxRates = new();

    // Rolling price history for VaR calculation: max 252 entries (1 trading year)
    private readonly ConcurrentDictionary<Guid, List<decimal>> _priceHistory = new();
    private readonly ConcurrentDictionary<Guid, object> _historyLocks = new();
    private const int MaxHistoryLength = 252;

    public void UpdatePrice(PriceTick tick)
    {
        _prices[tick.InstrumentId] = tick;

        var lockObj = _historyLocks.GetOrAdd(tick.InstrumentId, _ => new object());
        lock (lockObj)
        {
            var history = _priceHistory.GetOrAdd(tick.InstrumentId, _ => new List<decimal>());
            history.Add(tick.Price);
            if (history.Count > MaxHistoryLength)
                history.RemoveAt(0);
        }
    }

    public void UpdateFxRate(FxRate rate) => _fxRates[(rate.BaseCurrency, rate.QuoteCurrency)] = rate;

    public PriceTick? TryGetPrice(Guid instrumentId) =>
        _prices.TryGetValue(instrumentId, out var tick) ? tick : null;

    /// <summary>
    /// Gets FX rate for converting from base to quote currency.
    /// Returns 1.0 if base == quote (identity conversion).
    /// Returns 0 if rate not available.
    /// </summary>
    public decimal GetFxRate(Currency baseCurrency, Currency quoteCurrency)
    {
        if (baseCurrency == quoteCurrency) return 1m;

        if (_fxRates.TryGetValue((baseCurrency, quoteCurrency), out var rate))
            return rate.Rate;

        // Try inverse: if we have USD/EUR but need EUR/USD
        if (_fxRates.TryGetValue((quoteCurrency, baseCurrency), out var inverse))
            return 1m / inverse.Rate;

        return 0m;
    }

    /// <summary>
    /// Returns a snapshot copy of the price history for the given instrument.
    /// Returns null if no history has been recorded yet.
    /// Returns a copy to prevent callers mutating the internal buffer.
    /// </summary>
    public IReadOnlyList<decimal>? TryGetPriceHistory(Guid instrumentId)
    {
        if (!_priceHistory.TryGetValue(instrumentId, out var history)) return null;

        var lockObj = _historyLocks.GetOrAdd(instrumentId, _ => new object());
        lock (lockObj)
        {
            return history.ToList();
        }
    }

    public int PriceCount => _prices.Count;
    public int FxRateCount => _fxRates.Count;
}
