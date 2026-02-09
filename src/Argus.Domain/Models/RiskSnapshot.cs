namespace Argus.Domain.Models;

/// <summary>
/// Portfolio-level risk aggregate combining all position risks.
/// Published to Kafka topic: risk.snapshots
/// </summary>
public sealed record RiskSnapshot(
    DateTimeOffset Timestamp,
    IReadOnlyList<PositionRisk> Positions,
    decimal TotalUnrealizedPnlUsd,
    decimal TotalRealizedPnlUsd,
    decimal TotalNetPnlUsd,
    int PositionCount,
    int OpenPositionCount
);
