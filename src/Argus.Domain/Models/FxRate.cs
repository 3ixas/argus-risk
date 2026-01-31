using Argus.Domain.Enums;

namespace Argus.Domain.Models;

/// <summary>
/// Exchange rate between two currencies.
/// Rate represents: 1 unit of BaseCurrency = Rate units of QuoteCurrency
/// Example: EUR/USD = 1.08 means 1 EUR = 1.08 USD
/// Published to Kafka topic: market-data.fx
/// </summary>
public sealed record FxRate(
    Currency BaseCurrency,
    Currency QuoteCurrency,
    decimal Rate,
    DateTimeOffset Timestamp
);
