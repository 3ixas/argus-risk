using Argus.Domain.Enums;

namespace Argus.Domain.Models;

/// <summary>
/// Represents a trade execution (buy or sell of an instrument).
/// Immutable record flowing through Kafka from TradeSimulator to RiskEngine.
/// </summary>
public sealed record Trade(
    Guid TradeId,
    Guid InstrumentId,
    string Symbol,
    TradeSide Side,
    int Quantity,
    decimal Price,
    Currency Currency,
    DateTimeOffset Timestamp
);
