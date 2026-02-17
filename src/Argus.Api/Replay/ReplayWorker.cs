using System.Diagnostics.Metrics;
using Argus.Api.Hubs;
using Argus.Domain.Models;
using Argus.Infrastructure.Telemetry;
using Microsoft.AspNetCore.SignalR;

namespace Argus.Api.Replay;

/// <summary>
/// Background worker that streams historical snapshots to SignalR clients at the configured replay speed.
/// Uses IServiceScopeFactory to create scoped ReplayService instances for database queries.
/// </summary>
public sealed class ReplayWorker : BackgroundService
{
    private static readonly Counter<long> ReplaySessionsStarted =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.replay.sessions_started", description: "Total replay sessions started");

    private static readonly Counter<long> ReplaySnapshotsStreamed =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.replay.snapshots_streamed", description: "Total snapshots streamed during replay");

    private static readonly Histogram<double> ReplayDuration =
        ArgusDiagnostics.Meter.CreateHistogram<double>("argus.replay.duration_seconds", "s", "Duration of replay sessions");

    private readonly ReplaySession _session;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<RiskHub> _hubContext;
    private readonly ILogger<ReplayWorker> _logger;

    public ReplayWorker(
        ReplaySession session,
        IServiceScopeFactory scopeFactory,
        IHubContext<RiskHub> hubContext,
        ILogger<ReplayWorker> logger)
    {
        _session = session;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Replay worker started - waiting for replay sessions");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Poll for active replay session
            if (!_session.IsActive || _session.IsPaused)
            {
                await Task.Delay(100, stoppingToken);
                continue;
            }

            var state = _session.State;
            if (state is null)
            {
                await Task.Delay(100, stoppingToken);
                continue;
            }

            _logger.LogInformation(
                "Starting replay: {Start} → {End} at {Speed}x",
                state.StartTime, state.EndTime, state.Speed);

            ReplaySessionsStarted.Add(1);
            var replayStart = DateTimeOffset.UtcNow;
            var snapshotsStreamed = 0L;

            try
            {
                // Notify clients that replay has started
                await BroadcastReplayStatus(state, stoppingToken);

                while (_session.IsActive && !stoppingToken.IsCancellationRequested)
                {
                    // Handle pause
                    if (_session.IsPaused)
                    {
                        await BroadcastReplayStatus(_session.State!, stoppingToken);
                        await Task.Delay(100, stoppingToken);
                        continue;
                    }

                    state = _session.State!;

                    // Get next snapshot from database
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var replayService = scope.ServiceProvider.GetRequiredService<ReplayService>();
                    var nextSnapshot = await replayService.GetNextSnapshotAsync(
                        state.CurrentTime, state.EndTime, stoppingToken);

                    if (nextSnapshot is null)
                    {
                        // No more snapshots — end replay
                        _logger.LogInformation(
                            "Replay complete: streamed {Count} snapshots",
                            snapshotsStreamed);
                        _session.Stop();
                        await BroadcastReplayStatus(_session.State!, stoppingToken);
                        break;
                    }

                    // Calculate delay based on replay speed
                    var timeDelta = nextSnapshot.Timestamp - state.CurrentTime;
                    var realDelay = timeDelta / state.Speed;

                    // Apply delay (minimum 10ms to prevent tight loops)
                    if (realDelay > TimeSpan.FromMilliseconds(10))
                    {
                        await Task.Delay(realDelay, stoppingToken);
                    }

                    // Broadcast snapshot and advance position
                    await _hubContext.Clients.All.SendAsync(
                        "ReplayUpdate", nextSnapshot, stoppingToken);
                    _session.AdvanceTo(nextSnapshot.Timestamp);

                    snapshotsStreamed++;
                    ReplaySnapshotsStreamed.Add(1);

                    // Periodically broadcast status
                    if (snapshotsStreamed % 10 == 0)
                    {
                        await BroadcastReplayStatus(_session.State!, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during replay");
                _session.Stop();
                await BroadcastReplayStatus(_session.State!, stoppingToken);
            }
            finally
            {
                var duration = (DateTimeOffset.UtcNow - replayStart).TotalSeconds;
                ReplayDuration.Record(duration);
            }
        }

        _logger.LogInformation("Replay worker stopping");
    }

    private async Task BroadcastReplayStatus(ReplayState state, CancellationToken cancellationToken)
    {
        await _hubContext.Clients.All.SendAsync("ReplayStatus", new
        {
            isActive = state.IsActive,
            isPaused = state.IsPaused,
            currentTime = state.CurrentTime,
            startTime = state.StartTime,
            endTime = state.EndTime,
            speed = state.Speed
        }, cancellationToken);
    }
}
