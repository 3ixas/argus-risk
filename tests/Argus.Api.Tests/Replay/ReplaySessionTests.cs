using Argus.Api.Replay;
using FluentAssertions;
using Xunit;

namespace Argus.Api.Tests.Replay;

public sealed class ReplaySessionTests
{
    private static readonly DateTimeOffset StartTime = new(2024, 1, 15, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EndTime = new(2024, 1, 15, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void InitialState_IsInactive()
    {
        var session = new ReplaySession();

        session.IsActive.Should().BeFalse();
        session.IsPaused.Should().BeFalse();
        session.State.Should().BeNull();
    }

    [Fact]
    public void Start_TransitionsToActive()
    {
        var session = new ReplaySession();

        var result = session.Start(StartTime, EndTime, 5);

        result.Should().BeTrue();
        session.IsActive.Should().BeTrue();
        session.IsPaused.Should().BeFalse();
        session.State.Should().NotBeNull();
        session.State!.StartTime.Should().Be(StartTime);
        session.State!.EndTime.Should().Be(EndTime);
        session.State!.CurrentTime.Should().Be(StartTime);
        session.State!.Speed.Should().Be(5);
    }

    [Fact]
    public void Start_WhenAlreadyActive_ReturnsFalse()
    {
        var session = new ReplaySession();
        session.Start(StartTime, EndTime, 1);

        var result = session.Start(StartTime, EndTime, 10);

        result.Should().BeFalse();
        session.State!.Speed.Should().Be(1, "original session should be unchanged");
    }

    [Fact]
    public void Stop_TransitionsToInactive()
    {
        var session = new ReplaySession();
        session.Start(StartTime, EndTime, 1);

        session.Stop();

        session.IsActive.Should().BeFalse();
        session.State!.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Pause_TransitionsToPaused()
    {
        var session = new ReplaySession();
        session.Start(StartTime, EndTime, 1);

        session.Pause();

        session.IsPaused.Should().BeTrue();
        session.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Pause_WhenAlreadyPaused_NoOp()
    {
        var session = new ReplaySession();
        session.Start(StartTime, EndTime, 1);
        session.Pause();

        session.Pause(); // second pause

        session.IsPaused.Should().BeTrue();
    }

    [Fact]
    public void Pause_WhenNotActive_NoOp()
    {
        var session = new ReplaySession();

        session.Pause();

        session.State.Should().BeNull();
    }

    [Fact]
    public void Resume_TransitionsFromPaused()
    {
        var session = new ReplaySession();
        session.Start(StartTime, EndTime, 1);
        session.Pause();

        session.Resume();

        session.IsPaused.Should().BeFalse();
        session.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Resume_WhenNotPaused_NoOp()
    {
        var session = new ReplaySession();
        session.Start(StartTime, EndTime, 1);

        session.Resume();

        session.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void AdvanceTo_UpdatesCurrentTime()
    {
        var session = new ReplaySession();
        session.Start(StartTime, EndTime, 1);
        var newTime = StartTime.AddMinutes(5);

        session.AdvanceTo(newTime);

        session.State!.CurrentTime.Should().Be(newTime);
    }

    [Fact]
    public void AdvanceTo_WhenNotActive_NoOp()
    {
        var session = new ReplaySession();

        session.AdvanceTo(StartTime);

        session.State.Should().BeNull();
    }

    [Fact]
    public void FullLifecycle_StartPauseResumeStop()
    {
        var session = new ReplaySession();

        // Start
        session.Start(StartTime, EndTime, 10);
        session.IsActive.Should().BeTrue();
        session.IsPaused.Should().BeFalse();

        // Advance
        session.AdvanceTo(StartTime.AddMinutes(5));
        session.State!.CurrentTime.Should().Be(StartTime.AddMinutes(5));

        // Pause
        session.Pause();
        session.IsPaused.Should().BeTrue();

        // Resume
        session.Resume();
        session.IsPaused.Should().BeFalse();

        // Stop
        session.Stop();
        session.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentAccess_ThreadSafe()
    {
        var session = new ReplaySession();
        session.Start(StartTime, EndTime, 1);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var errors = 0;

        // Multiple threads advancing time concurrently
        var writers = Enumerable.Range(0, 4).Select(i => Task.Run(() =>
        {
            var offset = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    session.AdvanceTo(StartTime.AddSeconds(offset++));
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }
        })).ToArray();

        // Reader thread checking state consistency
        var reader = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var state = session.State;
                if (state is null)
                {
                    Interlocked.Increment(ref errors);
                    continue;
                }

                // State should be internally consistent
                if (!state.IsActive || state.Speed != 1)
                {
                    Interlocked.Increment(ref errors);
                }
            }
        });

        await Task.WhenAll([.. writers, reader]);

        errors.Should().Be(0, "concurrent access should be thread-safe");
    }

    [Fact]
    public void Start_AfterStop_CanRestartNewSession()
    {
        var session = new ReplaySession();
        session.Start(StartTime, EndTime, 1);
        session.Stop();

        var newStart = StartTime.AddHours(1);
        var newEnd = EndTime.AddHours(1);
        var result = session.Start(newStart, newEnd, 60);

        result.Should().BeTrue();
        session.IsActive.Should().BeTrue();
        session.State!.StartTime.Should().Be(newStart);
        session.State!.Speed.Should().Be(60);
    }
}
