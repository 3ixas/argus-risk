using Argus.Domain.Enums;

namespace Argus.Domain.Events;

public sealed record PositionOpened(
    Guid TradeId,
    Guid InstrumentId,
    string Symbol,
    Currency Currency,
    TradeSide Side,
    int Quantity,
    decimal Price,
    DateTimeOffset Timestamp
);
