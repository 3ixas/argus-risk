namespace Argus.Domain.Services;

/// <summary>
/// Pure static functions for Value at Risk (VaR) calculation.
/// No dependencies — all state passed as parameters for testability and determinism.
///
/// Two methods are provided to make model risk visible:
///   - Parametric: assumes normal return distribution; fast, clean
///   - Historical Simulation: uses actual observed returns; no distributional assumption
///
/// When the two methods diverge, it typically signals non-normal behaviour
/// (fat tails, skew) that parametric VaR would understate.
/// </summary>
public static class VaRCalculator
{
    /// <summary>
    /// Minimum number of price observations required to compute VaR.
    /// Fewer than 30 observations produce statistically unreliable estimates.
    /// </summary>
    public const int MinimumPrices = 30;

    /// <summary>
    /// Parametric (variance-covariance) VaR assuming normally distributed returns.
    /// Formula: VaR = -(mean - zScore × σ) × |positionValueUsd|
    ///
    /// The result is the expected maximum loss at (1 - confidence) probability.
    /// Returns null if insufficient price history is available.
    /// Returns 0 minimum (VaR cannot be a gain).
    /// </summary>
    /// <param name="prices">Ordered price series (oldest → newest).</param>
    /// <param name="positionValueUsd">Absolute position value in USD.</param>
    /// <param name="zScore">Standard normal quantile: 1.645 for 95%, 2.326 for 99%.</param>
    public static decimal? CalculateParametric(
        IReadOnlyList<decimal> prices,
        decimal positionValueUsd,
        double zScore)
    {
        if (prices == null || prices.Count < MinimumPrices) return null;

        var returns = ComputeReturns(prices);
        if (returns.Count < 2) return null;

        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / (returns.Count - 1);
        var stdDev = (decimal)Math.Sqrt((double)variance);

        var var95 = -(mean - (decimal)zScore * stdDev) * Math.Abs(positionValueUsd);
        return Math.Max(0m, var95);
    }

    /// <summary>
    /// Historical simulation VaR using the empirical return distribution.
    /// Picks the loss at the given quantile from actual observed returns — no normality assumption.
    ///
    /// Returns null if insufficient price history is available.
    /// Returns 0 minimum (VaR cannot be a gain).
    /// </summary>
    /// <param name="prices">Ordered price series (oldest → newest).</param>
    /// <param name="positionValueUsd">Absolute position value in USD.</param>
    /// <param name="quantile">Left-tail quantile: 0.05 for 95% VaR, 0.01 for 99% VaR.</param>
    public static decimal? CalculateHistorical(
        IReadOnlyList<decimal> prices,
        decimal positionValueUsd,
        double quantile)
    {
        if (prices == null || prices.Count < MinimumPrices) return null;

        var returns = ComputeReturns(prices);
        if (returns.Count < 2) return null;

        var sorted = returns.OrderBy(r => r).ToList();

        // Floor gives a conservative (larger loss) index — errs on the safe side
        var index = (int)Math.Floor(quantile * sorted.Count);
        index = Math.Clamp(index, 0, sorted.Count - 1);

        var var95 = -sorted[index] * Math.Abs(positionValueUsd);
        return Math.Max(0m, var95);
    }

    /// <summary>
    /// Expected Shortfall (CVaR) — the average loss in the worst (quantile × 100)% of days.
    /// Always ≥ the VaR at the same confidence level: it is the mean of the tail beyond the VaR cut.
    ///
    /// Used in Basel III/IV (FRTB) as the primary risk measure, replacing VaR.
    /// Returns null if insufficient price history is available.
    /// Returns 0 minimum (ES cannot be a gain).
    /// </summary>
    /// <param name="prices">Ordered price series (oldest → newest).</param>
    /// <param name="positionValueUsd">Absolute position value in USD.</param>
    /// <param name="quantile">Left-tail quantile: 0.05 for 95% ES, 0.01 for 99% ES.</param>
    public static decimal? CalculateExpectedShortfall(
        IReadOnlyList<decimal> prices,
        decimal positionValueUsd,
        double quantile)
    {
        if (prices == null || prices.Count < MinimumPrices) return null;

        var returns = ComputeReturns(prices);
        if (returns.Count < 2) return null;

        var sorted = returns.OrderBy(r => r).ToList();
        var cutIndex = (int)Math.Floor(quantile * sorted.Count);
        cutIndex = Math.Clamp(cutIndex, 0, sorted.Count);

        // Take the tail (returns worse than the VaR cut)
        var tailReturns = sorted.Take(cutIndex).ToList();
        if (tailReturns.Count == 0) return 0m;

        var tailMean = tailReturns.Average();
        var cvar = -tailMean * Math.Abs(positionValueUsd);
        return Math.Max(0m, cvar);
    }

    /// <summary>
    /// Computes log-close returns: r_t = (P_t - P_{t-1}) / P_{t-1}.
    /// Skips entries where the previous price is zero to avoid division by zero.
    /// </summary>
    private static List<decimal> ComputeReturns(IReadOnlyList<decimal> prices)
    {
        var returns = new List<decimal>(prices.Count - 1);
        for (var i = 1; i < prices.Count; i++)
        {
            if (prices[i - 1] != 0m)
                returns.Add((prices[i] - prices[i - 1]) / prices[i - 1]);
        }
        return returns;
    }
}
