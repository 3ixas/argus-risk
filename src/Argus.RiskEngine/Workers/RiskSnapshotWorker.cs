using System.Diagnostics;
using System.Diagnostics.Metrics;
using Argus.Domain.Models;
using Argus.Domain.Services;
using Argus.Infrastructure.Messaging;
using Argus.Infrastructure.Telemetry;
using Argus.RiskEngine.Caches;
using Marten;

namespace Argus.RiskEngine.Workers;

/// <summary>
/// Periodic worker that combines position state + market data to calculate
/// real-time risk snapshots, publishes them to Kafka, and persists to PostgreSQL for replay.
/// Runs on a 1-second cadence using PeriodicTimer.
/// </summary>
public sealed class RiskSnapshotWorker : BackgroundService
{
    private const string SnapshotTopic = "risk.snapshots";
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromSeconds(1);

    private static readonly Counter<long> SnapshotsPublishedCounter =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.risk.snapshots.published", description: "Total risk snapshots published");

    private static readonly Histogram<double> SnapshotDuration =
        ArgusDiagnostics.Meter.CreateHistogram<double>("argus.risk.snapshot.duration", "ms", "Snapshot build + publish latency");

    private readonly PositionCache _positionCache;
    private readonly MarketDataCache _marketDataCache;
    private readonly IMessageProducer<RiskSnapshot> _producer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RiskSnapshotWorker> _logger;

    private long _snapshotsPublished;

    public RiskSnapshotWorker(
        PositionCache positionCache,
        MarketDataCache marketDataCache,
        IMessageProducer<RiskSnapshot> producer,
        IServiceScopeFactory scopeFactory,
        ILogger<RiskSnapshotWorker> logger)
    {
        _positionCache = positionCache;
        _marketDataCache = marketDataCache;
        _producer = producer;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Risk snapshot worker starting - publishing every {Interval}s",
            SnapshotInterval.TotalSeconds);

        // Wait for market data and positions to populate
        await Task.Delay(3000, stoppingToken);

        using var timer = new PeriodicTimer(SnapshotInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var activity = ArgusDiagnostics.ActivitySource.StartActivity("risk.snapshot.cycle");
                var sw = Stopwatch.StartNew();

                var snapshot = BuildCurrentSnapshot();

                if (snapshot.PositionCount > 0)
                {
                    await _producer.ProduceAsync(
                        SnapshotTopic,
                        "portfolio",
                        snapshot,
                        stoppingToken);

                    // Persist to PostgreSQL for historical replay
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
                    session.Store(snapshot);
                    await session.SaveChangesAsync(stoppingToken);

                    _snapshotsPublished++;
                    SnapshotsPublishedCounter.Add(1);

                    sw.Stop();
                    SnapshotDuration.Record(sw.Elapsed.TotalMilliseconds);

                    activity?.SetTag("snapshot.positions", snapshot.PositionCount);

                    if (_snapshotsPublished % 10 == 0)
                    {
                        _logger.LogInformation(
                            "Risk snapshot #{Count}: {Positions} positions, " +
                            "unrealized={Unrealized:F2} USD, realized={Realized:F2} USD, net={Net:F2} USD",
                            _snapshotsPublished,
                            snapshot.OpenPositionCount,
                            snapshot.TotalUnrealizedPnlUsd,
                            snapshot.TotalRealizedPnlUsd,
                            snapshot.TotalNetPnlUsd);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building/publishing risk snapshot");
            }
        }

        _logger.LogInformation("Risk snapshot worker stopping - published {Count} snapshots",
            _snapshotsPublished);
    }

    private RiskSnapshot BuildCurrentSnapshot()
    {
        var positions = _positionCache.GetAll();

        var positionRisks = positions
            .Select(p => RiskCalculator.BuildPositionRisk(
                p,
                _marketDataCache.TryGetPrice(p.InstrumentId),
                _marketDataCache.GetFxRate))
            .Where(r => r != null)
            .Cast<PositionRisk>()
            .ToList();

        return RiskCalculator.BuildSnapshot(positionRisks, DateTimeOffset.UtcNow);
    }

    public long SnapshotsPublished => _snapshotsPublished;
}
