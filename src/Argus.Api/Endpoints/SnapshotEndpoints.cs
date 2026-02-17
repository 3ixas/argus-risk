using Argus.Api.Replay;

namespace Argus.Api.Endpoints;

/// <summary>
/// HTTP endpoints for querying historical risk snapshots.
/// </summary>
public static class SnapshotEndpoints
{
    public static void MapSnapshotEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/snapshots")
            .WithTags("Snapshots");

        group.MapGet("/", GetSnapshots);
        group.MapGet("/count", GetSnapshotCount);
    }

    /// <summary>
    /// Get snapshots within a time range.
    /// Query params: from (required), to (required)
    /// </summary>
    private static async Task<IResult> GetSnapshots(
        DateTimeOffset from,
        DateTimeOffset to,
        ReplayService replayService)
    {
        if (to <= from)
        {
            return Results.BadRequest(new { error = "'to' must be after 'from'" });
        }

        // Limit query range to prevent excessive data transfer
        var maxRange = TimeSpan.FromHours(1);
        if (to - from > maxRange)
        {
            return Results.BadRequest(new
            {
                error = $"Time range exceeds maximum of {maxRange.TotalMinutes} minutes"
            });
        }

        var snapshots = await replayService.GetSnapshotsInRangeAsync(from, to);
        return Results.Ok(snapshots);
    }

    /// <summary>
    /// Get the count of snapshots within a time range.
    /// Useful for progress indicators without transferring all data.
    /// </summary>
    private static async Task<IResult> GetSnapshotCount(
        DateTimeOffset from,
        DateTimeOffset to,
        ReplayService replayService)
    {
        if (to <= from)
        {
            return Results.BadRequest(new { error = "'to' must be after 'from'" });
        }

        var count = await replayService.GetSnapshotCountAsync(from, to);
        return Results.Ok(new { count });
    }
}
