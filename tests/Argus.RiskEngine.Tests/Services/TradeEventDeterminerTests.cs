using Argus.Domain.Aggregates;
using Argus.Domain.Enums;
using Argus.Domain.Events;
using Argus.Domain.Models;
using Argus.RiskEngine.Services;
using FluentAssertions;
using Xunit;

namespace Argus.RiskEngine.Tests.Services;

public sealed class TradeEventDeterminerTests
{
    private static readonly Guid InstrumentId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2025-01-15T10:00:00Z");

    private static Trade MakeTrade(TradeSide side, int qty, decimal price) =>
        new(Guid.NewGuid(), InstrumentId, "AAPL", side, qty, price, Currency.USD, Now);

    private static Position OpenLong(int qty, decimal price)
    {
        var p = new Position();
        p.Apply(new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", Currency.USD,
            TradeSide.Buy, qty, price, Now.AddMinutes(-1)));
        return p;
    }

    private static Position OpenShort(int qty, decimal price)
    {
        var p = new Position();
        p.Apply(new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", Currency.USD,
            TradeSide.Sell, qty, price, Now.AddMinutes(-1)));
        return p;
    }

    private static Position ClosedPosition()
    {
        var p = OpenLong(100, 150.00m);
        p.Apply(new PositionClosed(Guid.NewGuid(), 100, 160.00m, 1000.00m, Now.AddMinutes(-1)));
        return p;
    }

    // --- No existing position ---

    [Fact]
    public void NullPosition_Buy_ReturnsPositionOpened()
    {
        var trade = MakeTrade(TradeSide.Buy, 100, 150.00m);

        var result = TradeEventDeterminer.Determine(null, trade);

        var opened = result.Should().BeOfType<PositionOpened>().Subject;
        opened.Side.Should().Be(TradeSide.Buy);
        opened.Quantity.Should().Be(100);
        opened.Price.Should().Be(150.00m);
    }

    [Fact]
    public void NullPosition_Sell_ReturnsPositionOpened()
    {
        var trade = MakeTrade(TradeSide.Sell, 100, 150.00m);

        var result = TradeEventDeterminer.Determine(null, trade);

        var opened = result.Should().BeOfType<PositionOpened>().Subject;
        opened.Side.Should().Be(TradeSide.Sell);
    }

    [Fact]
    public void ClosedPosition_Trade_ReturnsPositionOpened()
    {
        var position = ClosedPosition();
        var trade = MakeTrade(TradeSide.Buy, 50, 155.00m);

        var result = TradeEventDeterminer.Determine(position, trade);

        result.Should().BeOfType<PositionOpened>();
    }

    // --- Same-side trades ---

    [Fact]
    public void Long_Buy_ReturnsPositionIncreased()
    {
        var position = OpenLong(100, 150.00m);
        var trade = MakeTrade(TradeSide.Buy, 50, 155.00m);

        var result = TradeEventDeterminer.Determine(position, trade);

        var increased = result.Should().BeOfType<PositionIncreased>().Subject;
        increased.Quantity.Should().Be(50);
        increased.Price.Should().Be(155.00m);
    }

    [Fact]
    public void Short_Sell_ReturnsPositionIncreased()
    {
        var position = OpenShort(100, 150.00m);
        var trade = MakeTrade(TradeSide.Sell, 50, 145.00m);

        var result = TradeEventDeterminer.Determine(position, trade);

        result.Should().BeOfType<PositionIncreased>();
    }

    // --- Opposite-side: partial close ---

    [Fact]
    public void Long_Sell_Partial_ReturnsPositionDecreased()
    {
        var position = OpenLong(100, 150.00m);
        var trade = MakeTrade(TradeSide.Sell, 60, 160.00m);

        var result = TradeEventDeterminer.Determine(position, trade);

        var decreased = result.Should().BeOfType<PositionDecreased>().Subject;
        decreased.QuantityClosed.Should().Be(60);
        decreased.RealizedPnl.Should().Be(600.00m); // (160 - 150) × 60
    }

    [Fact]
    public void Short_Buy_Partial_ReturnsPositionDecreased()
    {
        var position = OpenShort(100, 150.00m);
        var trade = MakeTrade(TradeSide.Buy, 40, 140.00m);

        var result = TradeEventDeterminer.Determine(position, trade);

        var decreased = result.Should().BeOfType<PositionDecreased>().Subject;
        decreased.QuantityClosed.Should().Be(40);
        decreased.RealizedPnl.Should().Be(400.00m); // (150 - 140) × 40
    }

    // --- Opposite-side: exact close ---

    [Fact]
    public void Long_Sell_Exact_ReturnsPositionClosed()
    {
        var position = OpenLong(100, 150.00m);
        var trade = MakeTrade(TradeSide.Sell, 100, 160.00m);

        var result = TradeEventDeterminer.Determine(position, trade);

        var closed = result.Should().BeOfType<PositionClosed>().Subject;
        closed.QuantityClosed.Should().Be(100);
        closed.RealizedPnl.Should().Be(1000.00m); // (160 - 150) × 100
    }

    [Fact]
    public void Short_Buy_Exact_ReturnsPositionClosed()
    {
        var position = OpenShort(100, 150.00m);
        var trade = MakeTrade(TradeSide.Buy, 100, 140.00m);

        var result = TradeEventDeterminer.Determine(position, trade);

        var closed = result.Should().BeOfType<PositionClosed>().Subject;
        closed.RealizedPnl.Should().Be(1000.00m); // (150 - 140) × 100
    }

    // --- Opposite-side: reversal ---

    [Fact]
    public void Long_Sell_Exceeds_ReturnsPositionReversed()
    {
        var position = OpenLong(100, 150.00m);
        var trade = MakeTrade(TradeSide.Sell, 150, 140.00m);

        var result = TradeEventDeterminer.Determine(position, trade);

        var reversed = result.Should().BeOfType<PositionReversed>().Subject;
        reversed.QuantityClosed.Should().Be(100);
        reversed.NewQuantity.Should().Be(50);
        reversed.NewSide.Should().Be(TradeSide.Sell);
        reversed.RealizedPnl.Should().Be(-1000.00m); // (150 - 140) × 100 loss for long
    }

    [Fact]
    public void Short_Buy_Exceeds_ReturnsPositionReversed()
    {
        var position = OpenShort(100, 150.00m);
        var trade = MakeTrade(TradeSide.Buy, 130, 160.00m);

        var result = TradeEventDeterminer.Determine(position, trade);

        var reversed = result.Should().BeOfType<PositionReversed>().Subject;
        reversed.QuantityClosed.Should().Be(100);
        reversed.NewQuantity.Should().Be(30);
        reversed.NewSide.Should().Be(TradeSide.Buy);
        reversed.RealizedPnl.Should().Be(-1000.00m); // (150 - 160) × 100 loss for short
    }
}
