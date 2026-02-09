using Argus.Domain.Aggregates;
using Argus.Domain.Enums;
using Argus.Domain.Events;
using Argus.Domain.Models;
using Argus.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Argus.RiskEngine.Tests.Services;

public sealed class RiskCalculatorTests
{
    private static readonly Guid InstrumentId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    // --- CalculateAverageCostBasis ---

    [Fact]
    public void AverageCostBasis_SingleLot_ReturnsLotPrice()
    {
        var lots = new List<CostLot> { new(100, 50.00m) };

        RiskCalculator.CalculateAverageCostBasis(lots).Should().Be(50.00m);
    }

    [Fact]
    public void AverageCostBasis_MultipleLots_ReturnsWeightedAverage()
    {
        var lots = new List<CostLot>
        {
            new(100, 10.00m), // 1000
            new(200, 20.00m), // 4000
        };

        // (1000 + 4000) / 300 = 16.6667
        var result = RiskCalculator.CalculateAverageCostBasis(lots);
        result.Should().BeApproximately(16.6667m, 0.0001m);
    }

    [Fact]
    public void AverageCostBasis_EmptyLots_ReturnsZero()
    {
        RiskCalculator.CalculateAverageCostBasis([]).Should().Be(0m);
    }

    // --- CalculateUnrealizedPnl ---

    [Fact]
    public void UnrealizedPnl_LongProfit_ReturnsPositive()
    {
        // Long 100 @ $50, now $55 → profit $500
        RiskCalculator.CalculateUnrealizedPnl(100, 50.00m, 55.00m).Should().Be(500.00m);
    }

    [Fact]
    public void UnrealizedPnl_LongLoss_ReturnsNegative()
    {
        // Long 100 @ $50, now $45 → loss -$500
        RiskCalculator.CalculateUnrealizedPnl(100, 50.00m, 45.00m).Should().Be(-500.00m);
    }

    [Fact]
    public void UnrealizedPnl_ShortProfit_ReturnsPositive()
    {
        // Short 100 @ $50, now $45 → profit $500
        // Signed qty = -100, (45-50) × -100 = 500
        RiskCalculator.CalculateUnrealizedPnl(-100, 50.00m, 45.00m).Should().Be(500.00m);
    }

    [Fact]
    public void UnrealizedPnl_ShortLoss_ReturnsNegative()
    {
        // Short 100 @ $50, now $55 → loss -$500
        RiskCalculator.CalculateUnrealizedPnl(-100, 50.00m, 55.00m).Should().Be(-500.00m);
    }

    [Fact]
    public void UnrealizedPnl_ZeroQuantity_ReturnsZero()
    {
        RiskCalculator.CalculateUnrealizedPnl(0, 50.00m, 55.00m).Should().Be(0m);
    }

    // --- ConvertToUsd ---

    [Fact]
    public void ConvertToUsd_AlreadyUsd_ReturnsAmount()
    {
        RiskCalculator.ConvertToUsd(100m, Currency.USD, (_, _) =>
            throw new InvalidOperationException("Should not be called"))
            .Should().Be(100m);
    }

    [Fact]
    public void ConvertToUsd_EurToUsd_AppliesRate()
    {
        // 1 EUR = 1.08 USD
        var result = RiskCalculator.ConvertToUsd(100m, Currency.EUR, (from, to) => 1.08m);
        result.Should().Be(108m);
    }

    [Fact]
    public void ConvertToUsd_GbpToUsd_AppliesRate()
    {
        var result = RiskCalculator.ConvertToUsd(100m, Currency.GBP, (from, to) => 1.27m);
        result.Should().Be(127m);
    }

    // --- BuildPositionRisk ---

    [Fact]
    public void BuildPositionRisk_NoPriceTick_ReturnsNull()
    {
        var position = CreatePosition(100, 50.00m, Currency.USD);

        RiskCalculator.BuildPositionRisk(position, null, (_, _) => 1m).Should().BeNull();
    }

    [Fact]
    public void BuildPositionRisk_WithPrice_ReturnsRisk()
    {
        var position = CreatePosition(100, 50.00m, Currency.USD);
        var price = new PriceTick(InstrumentId, "AAPL", 55.00m, Currency.USD, Now);

        var risk = RiskCalculator.BuildPositionRisk(position, price, (_, _) => 1m);

        risk.Should().NotBeNull();
        risk!.InstrumentId.Should().Be(InstrumentId);
        risk.Symbol.Should().Be("AAPL");
        risk.Side.Should().Be(TradeSide.Buy);
        risk.Quantity.Should().Be(100);
        risk.AverageCostBasis.Should().Be(50.00m);
        risk.CurrentPrice.Should().Be(55.00m);
        risk.UnrealizedPnl.Should().Be(500.00m);
        risk.UnrealizedPnlUsd.Should().Be(500.00m); // USD → no conversion
    }

    [Fact]
    public void BuildPositionRisk_NonUsdCurrency_ConvertsPnl()
    {
        var position = CreatePosition(100, 50.00m, Currency.EUR);
        var price = new PriceTick(InstrumentId, "SAP", 55.00m, Currency.EUR, Now);

        // EUR/USD = 1.10
        var risk = RiskCalculator.BuildPositionRisk(position, price, (_, _) => 1.10m);

        risk.Should().NotBeNull();
        risk!.UnrealizedPnl.Should().Be(500.00m);       // 500 EUR
        risk.UnrealizedPnlUsd.Should().Be(550.00m);     // 500 × 1.10
    }

    // --- BuildSnapshot ---

    [Fact]
    public void BuildSnapshot_AggregatesTotals()
    {
        var risks = new List<PositionRisk>
        {
            new(Guid.NewGuid(), "AAPL", Currency.USD, TradeSide.Buy, 100,
                50m, 55m, 500m, 500m, 100m, 100m),
            new(Guid.NewGuid(), "MSFT", Currency.USD, TradeSide.Buy, 50,
                200m, 210m, 500m, 500m, -50m, -50m),
        };

        var snapshot = RiskCalculator.BuildSnapshot(risks, Now);

        snapshot.TotalUnrealizedPnlUsd.Should().Be(1000m);
        snapshot.TotalRealizedPnlUsd.Should().Be(50m);
        snapshot.TotalNetPnlUsd.Should().Be(1050m);
        snapshot.PositionCount.Should().Be(2);
        snapshot.OpenPositionCount.Should().Be(2);
    }

    [Fact]
    public void BuildSnapshot_EmptyPortfolio_ReturnsZeros()
    {
        var snapshot = RiskCalculator.BuildSnapshot([], Now);

        snapshot.TotalUnrealizedPnlUsd.Should().Be(0m);
        snapshot.TotalRealizedPnlUsd.Should().Be(0m);
        snapshot.TotalNetPnlUsd.Should().Be(0m);
        snapshot.PositionCount.Should().Be(0);
        snapshot.OpenPositionCount.Should().Be(0);
    }

    // --- Helper: create a Position by replaying a PositionOpened event ---

    private static Position CreatePosition(int quantity, decimal price, Currency currency)
    {
        var position = new Position();
        var opened = new PositionOpened(
            Guid.NewGuid(), InstrumentId, "AAPL", currency, TradeSide.Buy, quantity, price, Now);
        position.Apply(opened);
        return position;
    }
}
