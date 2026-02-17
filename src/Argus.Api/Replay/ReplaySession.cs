namespace Argus.Api.Replay;

/// <summary>
/// Immutable state of an active replay session.
/// </summary>
public sealed record ReplayState(
    bool IsActive,
    bool IsPaused,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset CurrentTime,
    int Speed
);

/// <summary>
/// Thread-safe singleton holder for replay session state.
/// Single writer (ReplayWorker), multiple readers (endpoints, SignalR).
/// Uses volatile for visibility — same pattern as RiskSnapshotCache.
/// </summary>
public sealed class ReplaySession
{
    private volatile ReplayState? _state;
    private readonly object _lock = new();

    public bool IsActive => _state?.IsActive ?? false;
    public bool IsPaused => _state?.IsPaused ?? false;
    public ReplayState? State => _state;

    /// <summary>
    /// Start a new replay session. Fails if already active.
    /// </summary>
    public bool Start(DateTimeOffset startTime, DateTimeOffset endTime, int speed)
    {
        lock (_lock)
        {
            if (_state?.IsActive == true)
                return false;

            _state = new ReplayState(
                IsActive: true,
                IsPaused: false,
                StartTime: startTime,
                EndTime: endTime,
                CurrentTime: startTime,
                Speed: speed);

            return true;
        }
    }

    /// <summary>
    /// Stop the current replay session.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_state is not null)
            {
                _state = _state with { IsActive = false };
            }
        }
    }

    /// <summary>
    /// Pause the replay at the current position.
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            if (_state is { IsActive: true, IsPaused: false })
            {
                _state = _state with { IsPaused = true };
            }
        }
    }

    /// <summary>
    /// Resume a paused replay.
    /// </summary>
    public void Resume()
    {
        lock (_lock)
        {
            if (_state is { IsActive: true, IsPaused: true })
            {
                _state = _state with { IsPaused = false };
            }
        }
    }

    /// <summary>
    /// Advance the current time pointer. Called by ReplayWorker after broadcasting a snapshot.
    /// </summary>
    public void AdvanceTo(DateTimeOffset time)
    {
        lock (_lock)
        {
            if (_state is { IsActive: true })
            {
                _state = _state with { CurrentTime = time };
            }
        }
    }
}
