using Argus.Domain.Aggregates;
using Marten;

namespace Argus.Api.Endpoints;

public static class PositionEndpoints
{
    public static RouteGroupBuilder MapPositionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/positions");

        group.MapGet("/", async (IQuerySession session) =>
        {
            var positions = await session.Query<Position>()
                .Where(p => p.IsOpen)
                .ToListAsync();
            return Results.Ok(positions);
        })
        .WithTags("Positions")
        .WithSummary("Get all open positions");

        group.MapGet("/{instrumentId:guid}", async (Guid instrumentId, IQuerySession session) =>
        {
            var position = await session.LoadAsync<Position>(instrumentId);
            return position is null ? Results.NotFound() : Results.Ok(position);
        })
        .WithTags("Positions")
        .WithSummary("Get position by instrument ID");

        return group;
    }
}
