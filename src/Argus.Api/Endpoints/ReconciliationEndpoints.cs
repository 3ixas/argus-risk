using System.Diagnostics.Metrics;
using Argus.Api.Caches;
using Argus.Api.Services;
using Argus.Infrastructure.Telemetry;

namespace Argus.Api.Endpoints;

public static class ReconciliationEndpoints
{
    private static readonly Counter<long> ReconciliationRunsCounter =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.reconciliation.runs", description: "Total reconciliation runs");

    public static RouteGroupBuilder MapReconciliationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reconciliation");

        group.MapPost("/run", async (ReconciliationService service, ReconciliationCache cache) =>
        {
            var report = await service.RunAsync();
            cache.Update(report);

            var result = report.Passed ? "pass" : "fail";
            ReconciliationRunsCounter.Add(1, new KeyValuePair<string, object?>("result", result));

            return Results.Ok(report);
        });

        group.MapGet("/latest", (ReconciliationCache cache) =>
        {
            var report = cache.Latest;
            return report is null ? Results.NotFound() : Results.Ok(report);
        });

        return group;
    }
}
