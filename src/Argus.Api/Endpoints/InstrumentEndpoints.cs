using Argus.Infrastructure.Data;

namespace Argus.Api.Endpoints;

public static class InstrumentEndpoints
{
    public static RouteGroupBuilder MapInstrumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/instruments");

        group.MapGet("/", (InstrumentRepository repository) =>
            Results.Ok(repository.GetAll()));

        return group;
    }
}
