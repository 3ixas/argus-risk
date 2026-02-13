namespace Argus.Domain.Models;

public sealed record ReconciliationReport(
    DateTimeOffset Timestamp,
    int TotalEventsReplayed,
    string ExpectedChecksum,
    string ActualChecksum,
    bool Passed,
    IReadOnlyList<PositionDiscrepancy> Discrepancies
);

public sealed record PositionDiscrepancy(
    Guid InstrumentId,
    string Symbol,
    string Field,
    string Expected,
    string Actual,
    decimal? Difference
);
