using Argus.Api.Caches;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using FluentAssertions;
using Xunit;

namespace Argus.Api.Tests.Caches;

public sealed class AlertCacheTests
{
    private readonly AlertCache _cache = new();

    [Fact]
    public void GetActive_InitialState_ReturnsEmpty()
    {
        _cache.GetActive().Should().BeEmpty();
        _cache.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Update_UnresolvedAlert_AddsToCache()
    {
        _cache.Update(MakeAlert(AlertType.StaleData, "RiskEngine", isResolved: false));

        _cache.ActiveCount.Should().Be(1);
        _cache.GetActive().Should().ContainSingle()
            .Which.Type.Should().Be(AlertType.StaleData);
    }

    [Fact]
    public void Update_ResolvedAlert_RemovesFromCache()
    {
        _cache.Update(MakeAlert(AlertType.StaleData, "RiskEngine", isResolved: false));
        _cache.Update(MakeAlert(AlertType.StaleData, "RiskEngine", isResolved: true));

        _cache.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Update_ResolvedAlert_WhenNotPresent_DoesNotThrow()
    {
        var act = () => _cache.Update(MakeAlert(AlertType.StaleData, "RiskEngine", isResolved: true));
        act.Should().NotThrow();
        _cache.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Update_SameTypeAndComponent_Overwrites()
    {
        _cache.Update(MakeAlert(AlertType.StaleData, "RiskEngine", isResolved: false));
        var updated = MakeAlert(AlertType.StaleData, "RiskEngine", isResolved: false,
            message: "Updated message");
        _cache.Update(updated);

        _cache.ActiveCount.Should().Be(1);
        _cache.GetActive().Should().ContainSingle()
            .Which.Message.Should().Be("Updated message");
    }

    [Fact]
    public void GetActive_ReturnsNewestFirst()
    {
        var older = MakeAlert(AlertType.StaleData, "RiskEngine", isResolved: false,
            timestamp: DateTimeOffset.UtcNow.AddSeconds(-10));
        var newer = MakeAlert(AlertType.HighLatency, "RiskEngine", isResolved: false,
            timestamp: DateTimeOffset.UtcNow);

        _cache.Update(older);
        _cache.Update(newer);

        var results = _cache.GetActive();
        results[0].Type.Should().Be(AlertType.HighLatency);  // newer first
        results[1].Type.Should().Be(AlertType.StaleData);
    }

    [Fact]
    public async Task Update_ConcurrentAccess_NeverThrows()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var writers = Enumerable.Range(0, 4).Select(i => Task.Run(() =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var type = i % 2 == 0 ? AlertType.StaleData : AlertType.HighLatency;
                    var component = $"Worker{i}";
                    _cache.Update(MakeAlert(type, component, isResolved: false));
                    _cache.Update(MakeAlert(type, component, isResolved: true));
                }
            }
            catch (Exception ex) { exceptions.Add(ex); }
        })).ToArray();

        var reader = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
                _ = _cache.GetActive();
        });

        await Task.WhenAll([.. writers, reader]);

        exceptions.Should().BeEmpty("ConcurrentDictionary operations must never throw");
    }

    // --- Helpers ---

    private static Alert MakeAlert(
        AlertType type,
        string component,
        bool isResolved,
        string message = "Test alert",
        DateTimeOffset? timestamp = null) =>
        new(
            Id: Guid.NewGuid(),
            Type: type,
            Severity: AlertSeverity.Warning,
            Component: component,
            Message: message,
            Timestamp: timestamp ?? DateTimeOffset.UtcNow,
            IsResolved: isResolved,
            ResolvedAt: isResolved ? DateTimeOffset.UtcNow : null);
}
