namespace Argus.Domain.Models;

/// <summary>
/// Portfolio-level risk aggregate combining all position risks.
/// Published to Kafka topic: risk.snapshots and persisted to PostgreSQL for replay.
/// </summary>
public sealed record RiskSnapshot(
    Guid Id,
    DateTimeOffset Timestamp,
    IReadOnlyList<PositionRisk> Positions,
    decimal TotalUnrealizedPnlUsd,
    decimal TotalRealizedPnlUsd,
    decimal TotalNetPnlUsd,
    int PositionCount,
    int OpenPositionCount,
    int StalePositionCount,
    string DataQuality,
    decimal? PortfolioParametricVaR95,
    decimal? PortfolioParametricVaR99,
    decimal? PortfolioHistoricalVaR95,
    decimal? PortfolioHistoricalVaR99,
    decimal? PortfolioCVaR95,
    decimal? PortfolioCVaR99
);
