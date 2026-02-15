using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Argus.Infrastructure.Telemetry;

/// <summary>
/// Central holder for OpenTelemetry diagnostic primitives.
/// Static readonly instances are required — creating per-request causes duplicate registrations.
/// </summary>
public static class ArgusDiagnostics
{
    public const string ServiceName = "Argus";

    public static readonly Meter Meter = new(ServiceName);
    public static readonly ActivitySource ActivitySource = new(ServiceName);
}
