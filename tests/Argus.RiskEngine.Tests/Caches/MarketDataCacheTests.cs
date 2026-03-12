using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.RiskEngine.Caches;
using FluentAssertions;
using Xunit;

namespace Argus.RiskEngine.Tests.Caches;

public sealed class MarketDataCacheTests
{
    private static PriceTick Tick(Guid id, decimal price) =>
        new(id, "TEST", price, Currency.USD, DateTimeOffset.UtcNow);

    // --- Price history buffer ---

    [Fact]
    public void TryGetPriceHistory_UnknownInstrument_ReturnsNull()
    {
        var cache = new MarketDataCache();

        cache.TryGetPriceHistory(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void TryGetPriceHistory_AfterUpdates_ReturnsPricesInOrder()
    {
        var cache = new MarketDataCache();
        var id = Guid.NewGuid();

        cache.UpdatePrice(Tick(id, 100m));
        cache.UpdatePrice(Tick(id, 101m));
        cache.UpdatePrice(Tick(id, 102m));

        var history = cache.TryGetPriceHistory(id);
        history.Should().NotBeNull();
        history.Should().Equal(100m, 101m, 102m);
    }

    [Fact]
    public void TryGetPriceHistory_CapsAt252Entries_OldestDropped()
    {
        var cache = new MarketDataCache();
        var id = Guid.NewGuid();

        // Add 253 prices — the 254th push should drop the first
        for (var i = 1; i <= 253; i++)
            cache.UpdatePrice(Tick(id, (decimal)i));

        var history = cache.TryGetPriceHistory(id)!;
        history.Should().HaveCount(252);
        history[0].Should().Be(2m, "oldest entry (price=1) was evicted when 253rd was added");
        history[^1].Should().Be(253m, "newest entry is always preserved");
    }

    [Fact]
    public void TryGetPriceHistory_ReturnsSnapshot_NotLiveReference()
    {
        var cache = new MarketDataCache();
        var id = Guid.NewGuid();

        cache.UpdatePrice(Tick(id, 100m));
        var snapshot = cache.TryGetPriceHistory(id)!;

        // Adding a new price AFTER taking the snapshot should not affect the snapshot
        cache.UpdatePrice(Tick(id, 200m));

        snapshot.Should().HaveCount(1, "snapshot taken before second update should be immutable");
        cache.TryGetPriceHistory(id)!.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConcurrentPriceUpdates_DoNotCorruptHistory()
    {
        var cache = new MarketDataCache();
        var id = Guid.NewGuid();

        var tasks = Enumerable.Range(1, 100).Select(i =>
            Task.Run(() => cache.UpdatePrice(Tick(id, (decimal)i))));

        // Should never throw — ConcurrentDictionary + per-instrument lock prevents corruption
        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();

        var history = cache.TryGetPriceHistory(id);
        history.Should().NotBeNull();
        history!.Count.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(100);
    }
}
