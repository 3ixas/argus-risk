using System.Collections.Concurrent;
using Argus.Domain.Enums;
using Argus.Domain.Models;

namespace Argus.RiskEngine.Caches;

/// <summary>
/// Thread-safe in-memory store for latest market prices and FX rates.
/// Updated by PriceConsumerWorker and FxRateConsumerWorker.
/// Read by RiskSnapshotWorker for risk calculations.
/// Registered as a singleton.
/// </summary>
public sealed class MarketDataCache
{
    private readonly ConcurrentDictionary<Guid, PriceTick> _prices = new();
    private readonly ConcurrentDictionary<(Currency Base, Currency Quote), FxRate> _fxRates = new();

    public void UpdatePrice(PriceTick tick) => _prices[tick.InstrumentId] = tick;

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

    public int PriceCount => _prices.Count;
    public int FxRateCount => _fxRates.Count;
}
