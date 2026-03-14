using Argus.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Argus.RiskEngine.Tests.Services;

public sealed class VaRCalculatorTests
{
    // --- Helpers ---

    /// <summary>
    /// Builds a price series starting at basePrice, with a fixed daily return.
    /// Deterministic — no randomness — so tests produce exact expected values.
    /// </summary>
    private static List<decimal> ConstantReturnSeries(decimal basePrice, decimal dailyReturn, int days)
    {
        var prices = new List<decimal>(days) { basePrice };
        for (var i = 1; i < days; i++)
            prices.Add(prices[i - 1] * (1 + dailyReturn));
        return prices;
    }

    /// <summary>
    /// Builds a flat price series (zero volatility — all returns = 0).
    /// </summary>
    private static List<decimal> FlatPriceSeries(decimal price, int count) =>
        Enumerable.Repeat(price, count).ToList();

    // --- Insufficient history ---

    [Fact]
    public void CalculateParametric_NullPrices_ReturnsNull()
    {
        VaRCalculator.CalculateParametric(null!, 10_000m, 1.645).Should().BeNull();
    }

    [Fact]
    public void CalculateParametric_EmptyList_ReturnsNull()
    {
        VaRCalculator.CalculateParametric([], 10_000m, 1.645).Should().BeNull();
    }

    [Fact]
    public void CalculateParametric_Exactly29Prices_ReturnsNull()
    {
        // One below the 30-price minimum — no estimate should be produced
        var prices = FlatPriceSeries(100m, 29);
        VaRCalculator.CalculateParametric(prices, 10_000m, 1.645).Should().BeNull();
    }

    [Fact]
    public void CalculateParametric_Exactly30Prices_ReturnsValue()
    {
        var prices = ConstantReturnSeries(100m, 0.001m, 30);
        VaRCalculator.CalculateParametric(prices, 10_000m, 1.645).Should().NotBeNull();
    }

    [Fact]
    public void CalculateHistorical_NullPrices_ReturnsNull()
    {
        VaRCalculator.CalculateHistorical(null!, 10_000m, 0.05).Should().BeNull();
    }

    [Fact]
    public void CalculateHistorical_Exactly29Prices_ReturnsNull()
    {
        var prices = FlatPriceSeries(100m, 29);
        VaRCalculator.CalculateHistorical(prices, 10_000m, 0.05).Should().BeNull();
    }

    // --- Zero volatility ---

    [Fact]
    public void CalculateParametric_ZeroVolatility_ReturnsZero()
    {
        // All prices the same → all returns = 0 → stdDev = 0 → VaR = 0
        var prices = FlatPriceSeries(100m, 60);
        var result = VaRCalculator.CalculateParametric(prices, 10_000m, 1.645);
        result.Should().Be(0m);
    }

    [Fact]
    public void CalculateHistorical_ZeroVolatility_ReturnsZero()
    {
        // All prices the same → all returns = 0 → 5th-percentile return = 0 → VaR = 0
        var prices = FlatPriceSeries(100m, 60);
        var result = VaRCalculator.CalculateHistorical(prices, 10_000m, 0.05);
        result.Should().Be(0m);
    }

    // --- VaR is never negative ---

    [Fact]
    public void CalculateParametric_AllGainSeries_ReturnsZeroNotNegative()
    {
        // Strong uptrend: every return is highly positive
        // mean is high, stdDev is low → VaR would be negative raw → clamped to 0
        var prices = ConstantReturnSeries(100m, 0.05m, 60); // 5% daily gain
        var result = VaRCalculator.CalculateParametric(prices, 10_000m, 1.645);
        result.Should().Be(0m);
    }

    [Fact]
    public void CalculateHistorical_AllGainSeries_ReturnsZeroNotNegative()
    {
        var prices = ConstantReturnSeries(100m, 0.05m, 60);
        var result = VaRCalculator.CalculateHistorical(prices, 10_000m, 0.05);
        result.Should().Be(0m);
    }

    // --- Higher confidence = larger loss estimate ---

    [Fact]
    public void CalculateParametric_99PercentVaR_GreaterThan95Percent()
    {
        // Volatile series: mix of gains and losses
        var prices = new List<decimal>();
        decimal price = 100m;
        for (var i = 0; i < 60; i++)
        {
            var ret = i % 3 == 0 ? -0.02m : 0.01m; // two gains, one loss pattern
            price = price * (1 + ret);
            prices.Add(price);
        }

        var var95 = VaRCalculator.CalculateParametric(prices, 10_000m, 1.645);
        var var99 = VaRCalculator.CalculateParametric(prices, 10_000m, 2.326);

        var95.Should().NotBeNull();
        var99.Should().NotBeNull();
        var99!.Value.Should().BeGreaterThan(var95!.Value,
            "99% VaR covers a more extreme tail than 95% VaR");
    }

    [Fact]
    public void CalculateHistorical_99PercentVaR_GreaterThan95Percent()
    {
        var prices = new List<decimal>();
        decimal price = 100m;
        for (var i = 0; i < 60; i++)
        {
            var ret = i % 3 == 0 ? -0.02m : 0.01m;
            price = price * (1 + ret);
            prices.Add(price);
        }

        var var95 = VaRCalculator.CalculateHistorical(prices, 10_000m, 0.05);
        var var99 = VaRCalculator.CalculateHistorical(prices, 10_000m, 0.01);

        var95.Should().NotBeNull();
        var99.Should().NotBeNull();
        var99!.Value.Should().BeGreaterOrEqualTo(var95!.Value,
            "99% VaR is at least as large as 95% VaR");
    }

    // --- Larger position = proportionally larger VaR ---

    [Fact]
    public void CalculateParametric_DoublePosition_DoublesVaR()
    {
        var prices = ConstantReturnSeries(100m, -0.005m, 60);

        var var1x = VaRCalculator.CalculateParametric(prices, 10_000m, 1.645);
        var var2x = VaRCalculator.CalculateParametric(prices, 20_000m, 1.645);

        var1x.Should().NotBeNull();
        var2x.Should().NotBeNull();
        var2x!.Value.Should().BeApproximately(var1x!.Value * 2, 0.01m,
            "VaR scales linearly with position size");
    }

    [Fact]
    public void CalculateHistorical_DoublePosition_DoublesVaR()
    {
        var prices = ConstantReturnSeries(100m, -0.005m, 60);

        var var1x = VaRCalculator.CalculateHistorical(prices, 10_000m, 0.05);
        var var2x = VaRCalculator.CalculateHistorical(prices, 20_000m, 0.05);

        var1x.Should().NotBeNull();
        var2x.Should().NotBeNull();
        var2x!.Value.Should().BeApproximately(var1x!.Value * 2, 0.01m,
            "VaR scales linearly with position size");
    }

    // --- Expected Shortfall (CVaR) ---

    [Fact]
    public void CalculateExpectedShortfall_NullPrices_ReturnsNull()
    {
        VaRCalculator.CalculateExpectedShortfall(null!, 10_000m, 0.05).Should().BeNull();
    }

    [Fact]
    public void CalculateExpectedShortfall_InsufficientHistory_ReturnsNull()
    {
        // 29 prices — one below the 30-price minimum
        var prices = FlatPriceSeries(100m, 29);
        VaRCalculator.CalculateExpectedShortfall(prices, 10_000m, 0.05).Should().BeNull();
    }

    [Fact]
    public void CalculateExpectedShortfall_ZeroVolatility_ReturnsZero()
    {
        // Flat prices → all returns = 0 → tail mean = 0 → ES = 0
        var prices = FlatPriceSeries(100m, 60);
        var result = VaRCalculator.CalculateExpectedShortfall(prices, 10_000m, 0.05);
        result.Should().Be(0m);
    }

    [Fact]
    public void CalculateExpectedShortfall_95_GreaterThanOrEqualToVaR95()
    {
        // ES is the mean of the tail beyond VaR — so ES ≥ VaR at the same confidence level
        var prices = ConstantReturnSeries(100m, -0.005m, 60);

        var var95 = VaRCalculator.CalculateHistorical(prices, 10_000m, 0.05);
        var es95 = VaRCalculator.CalculateExpectedShortfall(prices, 10_000m, 0.05);

        var95.Should().NotBeNull();
        es95.Should().NotBeNull();
        es95!.Value.Should().BeGreaterOrEqualTo(var95!.Value,
            "ES (CVaR) is the average beyond the VaR threshold, so ES ≥ VaR");
    }

    [Fact]
    public void CalculateExpectedShortfall_LinearScalingWithPositionValue()
    {
        // ES should scale linearly with position size — same return distribution, doubled exposure
        var prices = ConstantReturnSeries(100m, -0.005m, 60);

        var es1x = VaRCalculator.CalculateExpectedShortfall(prices, 10_000m, 0.05);
        var es2x = VaRCalculator.CalculateExpectedShortfall(prices, 20_000m, 0.05);

        es1x.Should().NotBeNull();
        es2x.Should().NotBeNull();
        es2x!.Value.Should().BeApproximately(es1x!.Value * 2, 0.01m,
            "ES scales linearly with position size");
    }
}
