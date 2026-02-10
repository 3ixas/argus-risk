using Argus.Api.Caches;
using Argus.Api.Hubs;
using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace Argus.Api.Workers;

/// <summary>
/// Consumes RiskSnapshot messages from Kafka and:
/// 1. Updates the RiskSnapshotCache (for REST endpoint)
/// 2. Broadcasts to all SignalR clients (for real-time streaming)
/// </summary>
public sealed class RiskSnapshotConsumerWorker : BackgroundService
{
    private const string SnapshotTopic = "risk.snapshots";

    private readonly IMessageConsumer<RiskSnapshot> _consumer;
    private readonly RiskSnapshotCache _cache;
    private readonly IHubContext<RiskHub> _hubContext;
    private readonly ILogger<RiskSnapshotConsumerWorker> _logger;

    private long _snapshotsReceived;

    public RiskSnapshotConsumerWorker(
        IMessageConsumer<RiskSnapshot> consumer,
        RiskSnapshotCache cache,
        IHubContext<RiskHub> hubContext,
        ILogger<RiskSnapshotConsumerWorker> logger)
    {
        _consumer = consumer;
        _cache = cache;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Risk snapshot consumer starting - subscribing to {Topic}", SnapshotTopic);

        _consumer.Subscribe(SnapshotTopic);
        await Task.Delay(1000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                if (result == null) continue;

                _cache.Update(result.Value);
                _snapshotsReceived++;
                _consumer.Commit();

                await _hubContext.Clients.All.SendAsync(
                    "RiskUpdated", result.Value, stoppingToken);

                if (_snapshotsReceived % 10 == 0)
                {
                    _logger.LogInformation(
                        "Snapshot consumer: {Count} received, {Positions} positions, net P&L={Net:F2} USD",
                        _snapshotsReceived,
                        result.Value.OpenPositionCount,
                        result.Value.TotalNetPnlUsd);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming risk snapshot");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Risk snapshot consumer stopping - received {Count} snapshots", _snapshotsReceived);
    }

    public long SnapshotsReceived => _snapshotsReceived;
}
