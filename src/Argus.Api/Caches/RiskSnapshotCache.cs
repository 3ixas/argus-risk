using Argus.Domain.Models;

namespace Argus.Api.Caches;

/// <summary>
/// Thread-safe holder for the latest RiskSnapshot.
/// Single writer (Kafka consumer worker), multiple readers (REST endpoints, status).
/// Uses volatile for visibility — safe because RiskSnapshot is an immutable record.
/// </summary>
public sealed class RiskSnapshotCache
{
    private volatile RiskSnapshot? _latest;

    public RiskSnapshot? Latest => _latest;

    public void Update(RiskSnapshot snapshot) => _latest = snapshot;
}
