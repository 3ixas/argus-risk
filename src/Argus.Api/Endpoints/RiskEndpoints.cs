using Argus.Api.Caches;

namespace Argus.Api.Endpoints;

public static class RiskEndpoints
{
    public static RouteGroupBuilder MapRiskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/risk");

        group.MapGet("/snapshot", (RiskSnapshotCache cache) =>
        {
            var snapshot = cache.Latest;
            return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
        });

        return group;
    }
}
