using System.Diagnostics.Metrics;
using Argus.Api.Caches;
using Argus.Api.Hubs;
using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.Infrastructure.Telemetry;
using Microsoft.AspNetCore.SignalR;

namespace Argus.Api.Workers;

/// <summary>
/// Consumes Alert messages from Kafka topic: risk.alerts and:
/// 1. Updates AlertCache (active alerts for REST endpoint)
/// 2. Broadcasts "AlertReceived" to all SignalR clients (real-time UI updates)
/// </summary>
public sealed class AlertConsumerWorker : BackgroundService
{
    private const string AlertsTopic = "risk.alerts";

    private static readonly Counter<long> AlertsReceivedCounter =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.api.alerts.received",
            description: "Total alerts received by API");

    private readonly IMessageConsumer<Alert> _consumer;
    private readonly AlertCache _cache;
    private readonly IHubContext<RiskHub> _hubContext;
    private readonly ILogger<AlertConsumerWorker> _logger;

    private long _alertsReceived;

    public AlertConsumerWorker(
        IMessageConsumer<Alert> consumer,
        AlertCache cache,
        IHubContext<RiskHub> hubContext,
        ILogger<AlertConsumerWorker> logger)
    {
        _consumer = consumer;
        _cache = cache;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alert consumer starting - subscribing to {Topic}", AlertsTopic);

        _consumer.Subscribe(AlertsTopic);
        await Task.Delay(1000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                if (result == null) continue;

                var alert = result.Value;
                _cache.Update(alert);
                _alertsReceived++;
                AlertsReceivedCounter.Add(1);
                _consumer.Commit();

                await _hubContext.Clients.All.SendAsync("AlertReceived", alert, stoppingToken);

                _logger.LogDebug("Alert received: [{Severity}] {Type} on {Component} (resolved={Resolved})",
                    alert.Severity, alert.Type, alert.Component, alert.IsResolved);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming alert");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Alert consumer stopping - received {Count} alerts", _alertsReceived);
    }

    public long AlertsReceived => _alertsReceived;
}
