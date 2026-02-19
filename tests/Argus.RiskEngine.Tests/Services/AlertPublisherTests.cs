using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.RiskEngine.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Argus.RiskEngine.Tests.Services;

public sealed class AlertPublisherTests
{
    private readonly CapturingProducer _producer = new();
    private readonly AlertPublisher _publisher;

    public AlertPublisherTests()
    {
        _publisher = new AlertPublisher(_producer, NullLogger<AlertPublisher>.Instance);
    }

    [Fact]
    public async Task Raise_PublishesAlertToKafka()
    {
        await _publisher.RaiseAsync(AlertType.StaleData, AlertSeverity.Warning, "RiskEngine", "Prices stale");

        _producer.Published.Should().HaveCount(1);
        var alert = _producer.Published[0];
        alert.Type.Should().Be(AlertType.StaleData);
        alert.Severity.Should().Be(AlertSeverity.Warning);
        alert.Component.Should().Be("RiskEngine");
        alert.Message.Should().Be("Prices stale");
        alert.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task Raise_WithinDeduplicationWindow_SuppressesSecondAlert()
    {
        await _publisher.RaiseAsync(AlertType.StaleData, AlertSeverity.Warning, "RiskEngine", "First");
        await _publisher.RaiseAsync(AlertType.StaleData, AlertSeverity.Warning, "RiskEngine", "Second");

        // Only one alert published — second is deduplicated
        _producer.Published.Should().HaveCount(1);
    }

    [Fact]
    public async Task Raise_DifferentComponents_PublishesIndependently()
    {
        await _publisher.RaiseAsync(AlertType.StaleData, AlertSeverity.Warning, "RiskEngine", "Engine stale");
        await _publisher.RaiseAsync(AlertType.StaleData, AlertSeverity.Warning, "API", "API stale");

        _producer.Published.Should().HaveCount(2);
    }

    [Fact]
    public async Task Raise_DifferentTypes_PublishesIndependently()
    {
        await _publisher.RaiseAsync(AlertType.StaleData, AlertSeverity.Warning, "RiskEngine", "Stale");
        await _publisher.RaiseAsync(AlertType.HighLatency, AlertSeverity.Warning, "RiskEngine", "Slow");

        _producer.Published.Should().HaveCount(2);
    }

    [Fact]
    public async Task Resolve_WhenAlertActive_PublishesResolvedAlert()
    {
        await _publisher.RaiseAsync(AlertType.CircuitBreakerOpen, AlertSeverity.Error, "PriceConsumer", "Open");
        await _publisher.ResolveAsync(AlertType.CircuitBreakerOpen, "PriceConsumer");

        _producer.Published.Should().HaveCount(2);
        var resolved = _producer.Published[1];
        resolved.IsResolved.Should().BeTrue();
        resolved.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Resolve_WhenAlertNotActive_PublishesNothing()
    {
        // Resolving something never raised — no-op
        await _publisher.ResolveAsync(AlertType.StaleData, "RiskEngine");

        _producer.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAndRaise_AfterResolve_CanRaiseAgain()
    {
        await _publisher.RaiseAsync(AlertType.StaleData, AlertSeverity.Warning, "RiskEngine", "First");
        await _publisher.ResolveAsync(AlertType.StaleData, "RiskEngine");

        // After resolve, the dedup window is cleared — a new raise should publish
        await _publisher.RaiseAsync(AlertType.StaleData, AlertSeverity.Warning, "RiskEngine", "Second");

        _producer.Published.Should().HaveCount(3); // raise + resolve + re-raise
    }

    // --- Test double: captures produced messages in-memory ---

    private sealed class CapturingProducer : IMessageProducer<Alert>
    {
        public List<Alert> Published { get; } = [];

        public Task ProduceAsync(string topic, string? key, Alert value, CancellationToken cancellationToken = default)
        {
            Published.Add(value);
            return Task.CompletedTask;
        }

        public void Flush(TimeSpan timeout) { }
    }
}
