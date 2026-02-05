using Argus.Domain.Enums;

namespace Argus.Domain.Events;

public sealed record PositionReversed(
    Guid TradeId,
    int QuantityClosed,
    int NewQuantity,
    TradeSide NewSide,
    decimal ClosePrice,
    decimal RealizedPnl,
    decimal NewPositionPrice,
    DateTimeOffset Timestamp
);
