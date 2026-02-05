using Argus.Domain.Aggregates;
using Argus.Domain.Enums;
using Argus.Domain.Events;
using Argus.Domain.Models;
using FluentAssertions;
using Xunit;

namespace Argus.RiskEngine.Tests.Domain;

public sealed class PositionTests
{
    private static readonly Guid InstrumentId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2025-01-15T10:00:00Z");

    [Fact]
    public void Apply_PositionOpened_Buy_CreatesLongPosition()
    {
        var position = new Position();

        position.Apply(new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", Currency.USD,
            TradeSide.Buy, 100, 150.00m, Now));

        position.Id.Should().Be(InstrumentId);
        position.InstrumentId.Should().Be(InstrumentId);
        position.Symbol.Should().Be("AAPL");
        position.Currency.Should().Be(Currency.USD);
        position.Quantity.Should().Be(100);
        position.IsLong.Should().BeTrue();
        position.IsOpen.Should().BeTrue();
        position.RealizedPnl.Should().Be(0m);
        position.CostLots.Should().ContainSingle()
            .Which.Should().Be(new CostLot(100, 150.00m));
    }

    [Fact]
    public void Apply_PositionOpened_Sell_CreatesShortPosition()
    {
        var position = new Position();

        position.Apply(new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", Currency.USD,
            TradeSide.Sell, 100, 150.00m, Now));

        position.Quantity.Should().Be(-100);
        position.IsShort.Should().BeTrue();
        position.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void Apply_PositionIncreased_AddsQuantityAndLot()
    {
        var position = new Position();
        position.Apply(new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", Currency.USD,
            TradeSide.Buy, 100, 150.00m, Now));

        position.Apply(new PositionIncreased(
            Guid.NewGuid(), 50, 155.00m, Now.AddMinutes(1)));

        position.Quantity.Should().Be(150);
        position.CostLots.Should().HaveCount(2);
        position.CostLots[0].Should().Be(new CostLot(100, 150.00m));
        position.CostLots[1].Should().Be(new CostLot(50, 155.00m));
    }

    [Fact]
    public void Apply_PositionDecreased_ReducesQuantityAndAccumulatesPnl()
    {
        var position = new Position();
        position.Apply(new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", Currency.USD,
            TradeSide.Buy, 100, 150.00m, Now));

        position.Apply(new PositionDecreased(
            Guid.NewGuid(), 60, 160.00m, 600.00m, Now.AddMinutes(1)));

        position.Quantity.Should().Be(40);
        position.RealizedPnl.Should().Be(600.00m);
        position.IsOpen.Should().BeTrue();
        position.CostLots.Should().ContainSingle()
            .Which.Should().Be(new CostLot(40, 150.00m));
    }

    [Fact]
    public void Apply_PositionClosed_ZerosQuantityAndClearsLots()
    {
        var position = new Position();
        position.Apply(new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", Currency.USD,
            TradeSide.Buy, 100, 150.00m, Now));

        position.Apply(new PositionClosed(
            Guid.NewGuid(), 100, 160.00m, 1000.00m, Now.AddMinutes(1)));

        position.Quantity.Should().Be(0);
        position.RealizedPnl.Should().Be(1000.00m);
        position.IsOpen.Should().BeFalse();
        position.CostLots.Should().BeEmpty();
    }

    [Fact]
    public void Apply_PositionReversed_FlipsDirectionWithNewLot()
    {
        var position = new Position();
        position.Apply(new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", Currency.USD,
            TradeSide.Buy, 100, 150.00m, Now));

        position.Apply(new PositionReversed(
            Guid.NewGuid(), 100, 50, TradeSide.Sell,
            140.00m, -1000.00m, 140.00m, Now.AddMinutes(1)));

        position.Quantity.Should().Be(-50);
        position.IsShort.Should().BeTrue();
        position.IsOpen.Should().BeTrue();
        position.RealizedPnl.Should().Be(-1000.00m);
        position.CostLots.Should().ContainSingle()
            .Which.Should().Be(new CostLot(50, 140.00m));
    }

    [Fact]
    public void Apply_PositionOpened_AfterClose_ResetsState()
    {
        var position = new Position();

        // Open and close
        position.Apply(new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", Currency.USD,
            TradeSide.Buy, 100, 150.00m, Now));
        position.Apply(new PositionClosed(
            Guid.NewGuid(), 100, 160.00m, 1000.00m, Now.AddMinutes(1)));

        // Re-open
        position.Apply(new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", Currency.USD,
            TradeSide.Sell, 200, 170.00m, Now.AddMinutes(2)));

        position.Quantity.Should().Be(-200);
        position.IsShort.Should().BeTrue();
        position.IsOpen.Should().BeTrue();
        position.RealizedPnl.Should().Be(0m); // Reset on re-open
        position.CostLots.Should().ContainSingle()
            .Which.Should().Be(new CostLot(200, 170.00m));
    }
}
