namespace Argus.Domain.Events;

public sealed record PositionDecreased(
    Guid TradeId,
    int QuantityClosed,
    decimal Price,
    decimal RealizedPnl,
    DateTimeOffset Timestamp
);
