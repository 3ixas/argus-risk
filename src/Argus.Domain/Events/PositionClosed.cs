namespace Argus.Domain.Events;

public sealed record PositionClosed(
    Guid TradeId,
    int QuantityClosed,
    decimal Price,
    decimal RealizedPnl,
    DateTimeOffset Timestamp
);
