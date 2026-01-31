using Argus.Domain.Enums;

namespace Argus.Domain.Models;

/// <summary>
/// A single price update for an instrument at a point in time.
/// Published to Kafka topic: market-data.prices
/// </summary>
public sealed record PriceTick(
    Guid InstrumentId,
    string Symbol,
    decimal Price,
    Currency Currency,
    DateTimeOffset Timestamp
);
