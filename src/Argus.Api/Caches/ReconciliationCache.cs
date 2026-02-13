using Argus.Domain.Models;

namespace Argus.Api.Caches;

/// <summary>
/// Thread-safe holder for the latest ReconciliationReport.
/// Single writer (reconciliation endpoint), multiple readers (API + frontend).
/// Uses volatile for visibility — safe because ReconciliationReport is an immutable record.
/// </summary>
public sealed class ReconciliationCache
{
    private volatile ReconciliationReport? _latest;

    public ReconciliationReport? Latest => _latest;

    public void Update(ReconciliationReport report) => _latest = report;
}
