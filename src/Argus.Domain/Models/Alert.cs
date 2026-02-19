using Argus.Domain.Enums;

namespace Argus.Domain.Models;

/// <summary>
/// Represents a system alert for a detected fault condition.
/// Published to Kafka topic: risk.alerts and broadcast via SignalR.
/// </summary>
public sealed record Alert(
    Guid Id,
    AlertType Type,
    AlertSeverity Severity,
    string Component,
    string Message,
    DateTimeOffset Timestamp,
    bool IsResolved,
    DateTimeOffset? ResolvedAt
);
