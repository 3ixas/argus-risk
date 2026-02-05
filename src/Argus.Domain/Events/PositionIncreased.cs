namespace Argus.Domain.Events;

public sealed record PositionIncreased(
    Guid TradeId,
    int Quantity,
    decimal Price,
    DateTimeOffset Timestamp
);
