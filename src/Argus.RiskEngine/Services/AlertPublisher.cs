using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.Infrastructure.Telemetry;

namespace Argus.RiskEngine.Services;

/// <summary>
/// Publishes fault alerts to Kafka topic: risk.alerts.
/// Deduplicates raises within a 30-second window per (type, component) pair —
/// preventing alert floods when a condition persists (e.g. stale data every second).
/// </summary>
public sealed class AlertPublisher
{
    private const string AlertsTopic = "risk.alerts";
    private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromSeconds(30);

    private static readonly Counter<long> AlertsRaisedCounter =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.alerts.raised",
            description: "Total alerts raised (tagged by type and severity)");

    private static readonly Counter<long> AlertsResolvedCounter =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.alerts.resolved",
            description: "Total alerts resolved");

    private readonly IMessageProducer<Alert> _producer;
    private readonly ILogger<AlertPublisher> _logger;

    // Key: "{type}:{component}" → last raised timestamp
    private readonly ConcurrentDictionary<string, DateTimeOffset> _activeAlerts = new();

    public AlertPublisher(IMessageProducer<Alert> producer, ILogger<AlertPublisher> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    /// <summary>
    /// Raises an alert if not already active within the deduplication window.
    /// Key is "{type}:{component}" — same fault on different components raises independently.
    /// </summary>
    public async Task RaiseAsync(AlertType type, AlertSeverity severity, string component, string message)
    {
        var key = BuildKey(type, component);
        var now = DateTimeOffset.UtcNow;

        // Deduplicate: skip if the same alert was raised within the window
        if (_activeAlerts.TryGetValue(key, out var lastRaised) && now - lastRaised < DeduplicationWindow)
            return;

        _activeAlerts[key] = now;

        var alert = new Alert(
            Id: Guid.NewGuid(),
            Type: type,
            Severity: severity,
            Component: component,
            Message: message,
            Timestamp: now,
            IsResolved: false,
            ResolvedAt: null);

        await _producer.ProduceAsync(AlertsTopic, key, alert);

        AlertsRaisedCounter.Add(1,
            new KeyValuePair<string, object?>("type", type.ToString()),
            new KeyValuePair<string, object?>("severity", severity.ToString()));

        _logger.LogWarning("Alert raised [{Severity}] {Type} on {Component}: {Message}",
            severity, type, component, message);
    }

    /// <summary>
    /// Resolves an active alert, removing it from the deduplication window
    /// and publishing a resolved marker to Kafka.
    /// </summary>
    public async Task ResolveAsync(AlertType type, string component)
    {
        var key = BuildKey(type, component);

        if (!_activeAlerts.TryRemove(key, out _))
            return; // Not active — nothing to resolve

        var alert = new Alert(
            Id: Guid.NewGuid(),
            Type: type,
            Severity: AlertSeverity.Warning,
            Component: component,
            Message: $"{type} condition resolved on {component}",
            Timestamp: DateTimeOffset.UtcNow,
            IsResolved: true,
            ResolvedAt: DateTimeOffset.UtcNow);

        await _producer.ProduceAsync(AlertsTopic, key, alert);

        AlertsResolvedCounter.Add(1);

        _logger.LogInformation("Alert resolved: {Type} on {Component}", type, component);
    }

    private static string BuildKey(AlertType type, string component) => $"{type}:{component}";
}
