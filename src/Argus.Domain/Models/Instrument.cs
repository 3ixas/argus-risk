using Argus.Domain.Enums;

namespace Argus.Domain.Models;

/// <summary>
/// Represents a tradeable financial instrument (e.g., a stock).
/// Immutable reference data that defines what can be traded.
/// </summary>
public sealed record Instrument(
    Guid Id,
    string Symbol,
    string Name,
    Sector Sector,
    Currency Currency,
    decimal BasePrice
);
