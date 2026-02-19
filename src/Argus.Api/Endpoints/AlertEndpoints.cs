using Argus.Api.Caches;

namespace Argus.Api.Endpoints;

public static class AlertEndpoints
{
    public static RouteGroupBuilder MapAlertEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/alerts");

        group.MapGet("/", (AlertCache cache) =>
            Results.Ok(cache.GetActive()));

        group.MapGet("/count", (AlertCache cache) =>
            Results.Ok(new { count = cache.ActiveCount }));

        return group;
    }
}
