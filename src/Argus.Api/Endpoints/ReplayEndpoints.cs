using Argus.Api.Replay;

namespace Argus.Api.Endpoints;

/// <summary>
/// HTTP endpoints for replay session control.
/// </summary>
public static class ReplayEndpoints
{
    public static void MapReplayEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/replay")
            .WithTags("Replay");

        group.MapPost("/start", StartReplay).WithSummary("Start a historical replay session");
        group.MapPost("/stop", StopReplay).WithSummary("Stop the active replay session");
        group.MapPost("/pause", PauseReplay).WithSummary("Pause the active replay session");
        group.MapPost("/resume", ResumeReplay).WithSummary("Resume a paused replay session");
        group.MapGet("/status", GetReplayStatus).WithSummary("Get current replay session status");
        group.MapGet("/available-range", GetAvailableRange).WithSummary("Get earliest and latest available snapshot timestamps for replay");
    }

    /// <summary>
    /// Request body for starting a replay session.
    /// </summary>
    public sealed record StartReplayRequest(
        DateTimeOffset StartTime,
        DateTimeOffset EndTime,
        int Speed = 1
    );

    private static IResult StartReplay(StartReplayRequest request, ReplaySession session)
    {
        // Validate speed
        if (request.Speed is not (1 or 5 or 10 or 60))
        {
            return Results.BadRequest(new { error = "Speed must be 1, 5, 10, or 60" });
        }

        // Validate time range
        if (request.EndTime <= request.StartTime)
        {
            return Results.BadRequest(new { error = "EndTime must be after StartTime" });
        }

        var success = session.Start(request.StartTime, request.EndTime, request.Speed);
        if (!success)
        {
            return Results.Conflict(new { error = "A replay session is already active" });
        }

        return Results.Ok(new { message = "Replay started" });
    }

    private static IResult StopReplay(ReplaySession session)
    {
        if (!session.IsActive)
        {
            return Results.NotFound(new { error = "No active replay session" });
        }

        session.Stop();
        return Results.Ok(new { message = "Replay stopped" });
    }

    private static IResult PauseReplay(ReplaySession session)
    {
        if (!session.IsActive)
        {
            return Results.NotFound(new { error = "No active replay session" });
        }

        if (session.IsPaused)
        {
            return Results.BadRequest(new { error = "Replay is already paused" });
        }

        session.Pause();
        return Results.Ok(new { message = "Replay paused" });
    }

    private static IResult ResumeReplay(ReplaySession session)
    {
        if (!session.IsActive)
        {
            return Results.NotFound(new { error = "No active replay session" });
        }

        if (!session.IsPaused)
        {
            return Results.BadRequest(new { error = "Replay is not paused" });
        }

        session.Resume();
        return Results.Ok(new { message = "Replay resumed" });
    }

    private static IResult GetReplayStatus(ReplaySession session)
    {
        var state = session.State;
        if (state is null)
        {
            return Results.NotFound(new { error = "No replay session" });
        }

        return Results.Ok(new
        {
            isActive = state.IsActive,
            isPaused = state.IsPaused,
            startTime = state.StartTime,
            endTime = state.EndTime,
            currentTime = state.CurrentTime,
            speed = state.Speed
        });
    }

    private static async Task<IResult> GetAvailableRange(ReplayService replayService)
    {
        var range = await replayService.GetAvailableRangeAsync();
        if (range is null)
        {
            return Results.NotFound(new { error = "No snapshots available for replay" });
        }

        return Results.Ok(new
        {
            earliest = range.Value.Earliest,
            latest = range.Value.Latest
        });
    }
}
