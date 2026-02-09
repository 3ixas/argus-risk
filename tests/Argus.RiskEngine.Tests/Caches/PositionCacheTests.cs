using Argus.Domain.Aggregates;
using Argus.Domain.Enums;
using Argus.Domain.Events;
using Argus.RiskEngine.Caches;
using FluentAssertions;
using Xunit;

namespace Argus.RiskEngine.Tests.Caches;

public sealed class PositionCacheTests
{
    private readonly PositionCache _cache = new();

    [Fact]
    public void Update_OpenPosition_AddsToCache()
    {
        var position = CreatePosition(isOpen: true);

        _cache.Update(position);

        _cache.Count.Should().Be(1);
        _cache.GetAll().Should().ContainSingle().Which.InstrumentId.Should().Be(position.InstrumentId);
    }

    [Fact]
    public void Update_ClosedPosition_RemovesFromCache()
    {
        var id = Guid.NewGuid();
        var open = CreatePosition(instrumentId: id, isOpen: true);
        _cache.Update(open);
        _cache.Count.Should().Be(1);

        var closed = CreatePosition(instrumentId: id, isOpen: false);
        _cache.Update(closed);

        _cache.Count.Should().Be(0);
    }

    [Fact]
    public void Update_SameInstrument_ReplacesExisting()
    {
        var id = Guid.NewGuid();
        var first = CreatePosition(instrumentId: id, quantity: 100);
        var second = CreatePosition(instrumentId: id, quantity: 200);

        _cache.Update(first);
        _cache.Update(second);

        _cache.Count.Should().Be(1);
        _cache.GetAll().Should().ContainSingle().Which.Quantity.Should().Be(200);
    }

    [Fact]
    public void Remove_ExistingPosition_RemovesFromCache()
    {
        var position = CreatePosition(isOpen: true);
        _cache.Update(position);

        _cache.Remove(position.InstrumentId);

        _cache.Count.Should().Be(0);
    }

    [Fact]
    public void Remove_NonExistent_DoesNotThrow()
    {
        var act = () => _cache.Remove(Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Fact]
    public void GetAll_MultiplePositions_ReturnsAll()
    {
        _cache.Update(CreatePosition(instrumentId: Guid.NewGuid()));
        _cache.Update(CreatePosition(instrumentId: Guid.NewGuid()));
        _cache.Update(CreatePosition(instrumentId: Guid.NewGuid()));

        _cache.GetAll().Should().HaveCount(3);
    }

    [Fact]
    public void GetAll_ReturnsSnapshot_NotLiveReference()
    {
        _cache.Update(CreatePosition(instrumentId: Guid.NewGuid()));

        var snapshot = _cache.GetAll();
        _cache.Update(CreatePosition(instrumentId: Guid.NewGuid()));

        // Original snapshot should still have 1, not 2
        snapshot.Should().HaveCount(1);
        _cache.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public async Task ConcurrentUpdates_DoNotCorrupt()
    {
        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => _cache.Update(CreatePosition(instrumentId: Guid.NewGuid()))));

        await Task.WhenAll(tasks);

        _cache.Count.Should().Be(100);
    }

    // --- Helper ---

    private static Position CreatePosition(
        Guid? instrumentId = null,
        int quantity = 100,
        bool isOpen = true)
    {
        var id = instrumentId ?? Guid.NewGuid();
        var position = new Position();

        var opened = new PositionOpened(
            Guid.NewGuid(), id, "TEST", Currency.USD, TradeSide.Buy, quantity, 50.00m,
            DateTimeOffset.UtcNow);
        position.Apply(opened);

        if (!isOpen)
        {
            var closed = new PositionClosed(
                Guid.NewGuid(), quantity, 55.00m, 500m,
                DateTimeOffset.UtcNow);
            position.Apply(closed);
        }

        return position;
    }
}
