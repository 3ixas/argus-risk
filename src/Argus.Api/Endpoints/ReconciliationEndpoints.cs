using Argus.Api.Caches;
using Argus.Api.Services;

namespace Argus.Api.Endpoints;

public static class ReconciliationEndpoints
{
    public static RouteGroupBuilder MapReconciliationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reconciliation");

        group.MapPost("/run", async (ReconciliationService service, ReconciliationCache cache) =>
        {
            var report = await service.RunAsync();
            cache.Update(report);
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
