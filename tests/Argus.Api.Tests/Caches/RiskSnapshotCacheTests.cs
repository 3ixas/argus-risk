using Argus.Api.Caches;
using Argus.Domain.Models;
using FluentAssertions;
using Xunit;

namespace Argus.Api.Tests.Caches;

public sealed class RiskSnapshotCacheTests
{
    private static RiskSnapshot CreateSnapshot(int positionCount = 5, decimal netPnl = 1000m) =>
        new(
            Timestamp: DateTimeOffset.UtcNow,
            Positions: [],
            TotalUnrealizedPnlUsd: netPnl * 0.6m,
            TotalRealizedPnlUsd: netPnl * 0.4m,
            TotalNetPnlUsd: netPnl,
            PositionCount: positionCount,
            OpenPositionCount: positionCount
        );

    [Fact]
    public void Latest_InitialState_ReturnsNull()
    {
        var cache = new RiskSnapshotCache();

        cache.Latest.Should().BeNull();
    }

    [Fact]
    public void Update_StoresSnapshot()
    {
        var cache = new RiskSnapshotCache();
        var snapshot = CreateSnapshot();

        cache.Update(snapshot);

        cache.Latest.Should().BeSameAs(snapshot);
    }

    [Fact]
    public void Update_MultipleUpdates_OnlyLatestRetained()
    {
        var cache = new RiskSnapshotCache();
        var first = CreateSnapshot(positionCount: 1);
        var second = CreateSnapshot(positionCount: 2);
        var third = CreateSnapshot(positionCount: 3);

        cache.Update(first);
        cache.Update(second);
        cache.Update(third);

        cache.Latest.Should().BeSameAs(third);
        cache.Latest!.PositionCount.Should().Be(3);
    }

    [Fact]
    public async Task Update_ConcurrentReadsAndWrites_NeverReturnsCorruptedSnapshot()
    {
        var cache = new RiskSnapshotCache();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var readErrors = 0;

        // Writer thread: continuously updates with snapshots
        var writer = Task.Run(() =>
        {
            var count = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                cache.Update(CreateSnapshot(positionCount: ++count));
            }
        });

        // Reader threads: continuously read and verify consistency
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var snapshot = cache.Latest;
                if (snapshot is not null)
                {
                    // Verify snapshot is internally consistent (not a torn read)
                    if (snapshot.PositionCount != snapshot.OpenPositionCount)
                        Interlocked.Increment(ref readErrors);
                }
            }
        })).ToArray();

        await Task.WhenAll([writer, .. readers]);

        readErrors.Should().Be(0, "volatile reference assignment ensures atomic snapshot visibility");
    }
}
