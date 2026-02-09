using Argus.Domain.Enums;

namespace Argus.Domain.Models;

/// <summary>
/// Per-position risk metrics combining position state with live market data.
/// Computed by RiskCalculator — a point-in-time snapshot, not event-sourced.
/// </summary>
public sealed record PositionRisk(
    Guid InstrumentId,
    string Symbol,
    Currency Currency,
    TradeSide Side,
    int Quantity,
    decimal AverageCostBasis,
    decimal CurrentPrice,
    decimal UnrealizedPnl,
    decimal UnrealizedPnlUsd,
    decimal RealizedPnl,
    decimal RealizedPnlUsd
);
