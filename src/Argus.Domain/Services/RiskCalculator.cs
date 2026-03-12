using Argus.Domain.Aggregates;
using Argus.Domain.Enums;
using Argus.Domain.Models;

namespace Argus.Domain.Services;

/// <summary>
/// Pure static functions for calculating risk metrics.
/// No dependencies — all state passed as parameters for testability.
/// </summary>
public static class RiskCalculator
{
    /// <summary>
    /// Price data older than this threshold is considered stale.
    /// Five seconds gives market data feed a generous window while still detecting outages.
    /// </summary>
    public static readonly TimeSpan StalenessThreshold = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Weighted average cost across all cost lots.
    /// Returns 0 if lots are empty.
    /// </summary>
    public static decimal CalculateAverageCostBasis(List<CostLot> lots)
    {
        if (lots.Count == 0) return 0m;

        var totalQty = lots.Sum(l => l.Quantity);
        if (totalQty == 0) return 0m;

        return lots.Sum(l => l.Quantity * l.PricePerUnit) / totalQty;
    }

    /// <summary>
    /// Unrealized P&amp;L using signed quantity convention.
    /// Long (qty > 0): profit when price rises. Short (qty &lt; 0): profit when price falls.
    /// Formula: (currentPrice - avgCost) × quantity
    /// </summary>
    public static decimal CalculateUnrealizedPnl(int quantity, decimal avgCost, decimal currentPrice)
    {
        return (currentPrice - avgCost) * quantity;
    }

    /// <summary>
    /// Converts an amount to USD using the provided FX lookup.
    /// If the amount is already in USD, returns as-is (no lookup needed).
    /// </summary>
    public static decimal ConvertToUsd(decimal amount, Currency from, Func<Currency, Currency, decimal> fxRateLookup)
    {
        if (from == Currency.USD) return amount;
        var rate = fxRateLookup(from, Currency.USD);
        return amount * rate;
    }

    /// <summary>
    /// Builds a PositionRisk snapshot for a single position.
    /// Returns null if no current market price is available (position skipped in snapshot).
    /// </summary>
    /// <param name="now">Current time used to determine if price data is stale.</param>
    /// <param name="priceHistory">Optional price series for VaR calculation (oldest → newest).
    /// Pass null (or fewer than 30 prices) to omit VaR — fields will be null in the output.</param>
    public static PositionRisk? BuildPositionRisk(
        Position position,
        PriceTick? currentPrice,
        Func<Currency, Currency, decimal> fxRateLookup,
        DateTimeOffset now,
        IReadOnlyList<decimal>? priceHistory = null)
    {
        if (currentPrice == null) return null;

        var avgCost = CalculateAverageCostBasis(position.CostLots);
        var unrealizedPnl = CalculateUnrealizedPnl(position.Quantity, avgCost, currentPrice.Price);
        var unrealizedPnlUsd = ConvertToUsd(unrealizedPnl, position.Currency, fxRateLookup);
        var realizedPnlUsd = ConvertToUsd(position.RealizedPnl, position.Currency, fxRateLookup);

        var priceAge = now - currentPrice.Timestamp;
        var isStale = priceAge > StalenessThreshold;

        // VaR uses absolute position value in USD — direction-agnostic loss metric
        var positionValueUsd = Math.Abs(ConvertToUsd(
            currentPrice.Price * Math.Abs(position.Quantity),
            position.Currency,
            fxRateLookup));

        var parametricVaR95 = VaRCalculator.CalculateParametric(priceHistory!, positionValueUsd, 1.645);
        var parametricVaR99 = VaRCalculator.CalculateParametric(priceHistory!, positionValueUsd, 2.326);
        var historicalVaR95 = VaRCalculator.CalculateHistorical(priceHistory!, positionValueUsd, 0.05);
        var historicalVaR99 = VaRCalculator.CalculateHistorical(priceHistory!, positionValueUsd, 0.01);

        return new PositionRisk(
            InstrumentId: position.InstrumentId,
            Symbol: position.Symbol,
            Currency: position.Currency,
            Side: position.Quantity >= 0 ? TradeSide.Buy : TradeSide.Sell,
            Quantity: Math.Abs(position.Quantity),
            AverageCostBasis: avgCost,
            CurrentPrice: currentPrice.Price,
            UnrealizedPnl: unrealizedPnl,
            UnrealizedPnlUsd: unrealizedPnlUsd,
            RealizedPnl: position.RealizedPnl,
            RealizedPnlUsd: realizedPnlUsd,
            IsStale: isStale,
            PriceAgeSeconds: priceAge.TotalSeconds,
            ParametricVaR95: parametricVaR95,
            ParametricVaR99: parametricVaR99,
            HistoricalVaR95: historicalVaR95,
            HistoricalVaR99: historicalVaR99);
    }

    /// <summary>
    /// Aggregates individual position risks into a portfolio-level snapshot.
    /// Each snapshot gets a unique Id for Marten document storage.
    /// DataQuality: "Good" if 0% stale, "Degraded" if less than 25%, "Stale" if 25%+.
    ///
    /// Portfolio VaR is the undiversified sum of individual position VaRs.
    /// A null portfolio VaR means no positions have sufficient price history yet.
    /// </summary>
    public static RiskSnapshot BuildSnapshot(IEnumerable<PositionRisk> positionRisks, DateTimeOffset timestamp)
    {
        var positions = positionRisks.ToList();

        var totalUnrealizedUsd = positions.Sum(p => p.UnrealizedPnlUsd);
        var totalRealizedUsd = positions.Sum(p => p.RealizedPnlUsd);
        var staleCount = positions.Count(p => p.IsStale);

        var dataQuality = positions.Count == 0
            ? "Good"
            : staleCount == 0
                ? "Good"
                : (double)staleCount / positions.Count < 0.25
                    ? "Degraded"
                    : "Stale";

        // Sum VaRs across positions — null if no position has sufficient history
        var portfolioParamVaR95 = positions.Any(p => p.ParametricVaR95.HasValue)
            ? positions.Sum(p => p.ParametricVaR95 ?? 0m)
            : (decimal?)null;
        var portfolioParamVaR99 = positions.Any(p => p.ParametricVaR99.HasValue)
            ? positions.Sum(p => p.ParametricVaR99 ?? 0m)
            : (decimal?)null;
        var portfolioHistVaR95 = positions.Any(p => p.HistoricalVaR95.HasValue)
            ? positions.Sum(p => p.HistoricalVaR95 ?? 0m)
            : (decimal?)null;
        var portfolioHistVaR99 = positions.Any(p => p.HistoricalVaR99.HasValue)
            ? positions.Sum(p => p.HistoricalVaR99 ?? 0m)
            : (decimal?)null;

        return new RiskSnapshot(
            Id: Guid.NewGuid(),
            Timestamp: timestamp,
            Positions: positions,
            TotalUnrealizedPnlUsd: totalUnrealizedUsd,
            TotalRealizedPnlUsd: totalRealizedUsd,
            TotalNetPnlUsd: totalUnrealizedUsd + totalRealizedUsd,
            PositionCount: positions.Count,
            OpenPositionCount: positions.Count(p => p.Quantity > 0),
            StalePositionCount: staleCount,
            DataQuality: dataQuality,
            PortfolioParametricVaR95: portfolioParamVaR95,
            PortfolioParametricVaR99: portfolioParamVaR99,
            PortfolioHistoricalVaR95: portfolioHistVaR95,
            PortfolioHistoricalVaR99: portfolioHistVaR99);
    }
}
