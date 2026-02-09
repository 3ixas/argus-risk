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
    public static PositionRisk? BuildPositionRisk(
        Position position,
        PriceTick? currentPrice,
        Func<Currency, Currency, decimal> fxRateLookup)
    {
        if (currentPrice == null) return null;

        var avgCost = CalculateAverageCostBasis(position.CostLots);
        var unrealizedPnl = CalculateUnrealizedPnl(position.Quantity, avgCost, currentPrice.Price);
        var unrealizedPnlUsd = ConvertToUsd(unrealizedPnl, position.Currency, fxRateLookup);
        var realizedPnlUsd = ConvertToUsd(position.RealizedPnl, position.Currency, fxRateLookup);

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
            RealizedPnlUsd: realizedPnlUsd);
    }

    /// <summary>
    /// Aggregates individual position risks into a portfolio-level snapshot.
    /// </summary>
    public static RiskSnapshot BuildSnapshot(IEnumerable<PositionRisk> positionRisks, DateTimeOffset timestamp)
    {
        var positions = positionRisks.ToList();

        var totalUnrealizedUsd = positions.Sum(p => p.UnrealizedPnlUsd);
        var totalRealizedUsd = positions.Sum(p => p.RealizedPnlUsd);

        return new RiskSnapshot(
            Timestamp: timestamp,
            Positions: positions,
            TotalUnrealizedPnlUsd: totalUnrealizedUsd,
            TotalRealizedPnlUsd: totalRealizedUsd,
            TotalNetPnlUsd: totalUnrealizedUsd + totalRealizedUsd,
            PositionCount: positions.Count,
            OpenPositionCount: positions.Count(p => p.Quantity > 0));
    }
}
