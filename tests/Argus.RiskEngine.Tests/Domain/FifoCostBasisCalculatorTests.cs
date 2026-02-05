using Argus.Domain.Models;
using Argus.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Argus.RiskEngine.Tests.Domain;

public sealed class FifoCostBasisCalculatorTests
{
    [Fact]
    public void SingleLot_FullClose_ReturnsCorrectPnl()
    {
        var lots = new List<CostLot> { new(100, 50.00m) };

        var result = FifoCostBasisCalculator.Calculate(lots, 100, 55.00m, isLong: true);

        result.RealizedPnl.Should().Be(500.00m); // (55 - 50) × 100
        result.RemainingLots.Should().BeEmpty();
    }

    [Fact]
    public void SingleLot_PartialClose_ReturnsPnlAndRemainder()
    {
        var lots = new List<CostLot> { new(100, 50.00m) };

        var result = FifoCostBasisCalculator.Calculate(lots, 60, 55.00m, isLong: true);

        result.RealizedPnl.Should().Be(300.00m); // (55 - 50) × 60
        result.RemainingLots.Should().ContainSingle()
            .Which.Should().Be(new CostLot(40, 50.00m));
    }

    [Fact]
    public void MultiLot_FifoOrder_ConsumesOldestFirst()
    {
        var lots = new List<CostLot>
        {
            new(100, 10.00m), // oldest — consumed first
            new(50, 12.00m),  // second
        };

        var result = FifoCostBasisCalculator.Calculate(lots, 120, 15.00m, isLong: true);

        // 100 @ $10 → (15-10)×100 = 500
        //  20 @ $12 → (15-12)× 20 =  60
        result.RealizedPnl.Should().Be(560.00m);
        result.RemainingLots.Should().ContainSingle()
            .Which.Should().Be(new CostLot(30, 12.00m));
    }

    [Fact]
    public void MultiLot_FullConsumption_ReturnsEmptyRemaining()
    {
        var lots = new List<CostLot>
        {
            new(100, 10.00m),
            new(50, 12.00m),
        };

        var result = FifoCostBasisCalculator.Calculate(lots, 150, 15.00m, isLong: true);

        // 100 @ $10 → (15-10)×100 = 500
        //  50 @ $12 → (15-12)× 50 = 150
        result.RealizedPnl.Should().Be(650.00m);
        result.RemainingLots.Should().BeEmpty();
    }

    [Fact]
    public void ShortPosition_PnlIsInverted()
    {
        // Short sold at $50, buying back at $45 = profit
        var lots = new List<CostLot> { new(100, 50.00m) };

        var result = FifoCostBasisCalculator.Calculate(lots, 100, 45.00m, isLong: false);

        result.RealizedPnl.Should().Be(500.00m); // (50 - 45) × 100
        result.RemainingLots.Should().BeEmpty();
    }

    [Fact]
    public void ZeroPnl_CloseAtSamePrice()
    {
        var lots = new List<CostLot> { new(100, 50.00m) };

        var result = FifoCostBasisCalculator.Calculate(lots, 100, 50.00m, isLong: true);

        result.RealizedPnl.Should().Be(0m);
    }

    [Fact]
    public void LargeQuantity_NoOverflow()
    {
        var lots = new List<CostLot> { new(1_000_000, 500.00m) };

        var result = FifoCostBasisCalculator.Calculate(lots, 1_000_000, 510.00m, isLong: true);

        result.RealizedPnl.Should().Be(10_000_000.00m); // (510 - 500) × 1M
    }

    [Fact]
    public void SmallPrices_PrecisionPreserved()
    {
        var lots = new List<CostLot> { new(100, 0.01m) };

        var result = FifoCostBasisCalculator.Calculate(lots, 100, 0.02m, isLong: true);

        result.RealizedPnl.Should().Be(1.00m); // (0.02 - 0.01) × 100
    }
}
