using Argus.Api.Caches;

namespace Argus.Api.Endpoints;

public static class AlertEndpoints
{
    public static RouteGroupBuilder MapAlertEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/alerts");

        group.MapGet("/", (AlertCache cache) =>
            Results.Ok(cache.GetActive()))
            .WithTags("Alerts")
            .WithSummary("Get all active alerts");

        group.MapGet("/count", (AlertCache cache) =>
            Results.Ok(new { count = cache.ActiveCount }))
            .WithTags("Alerts")
            .WithSummary("Get count of active alerts");

        return group;
    }
}
