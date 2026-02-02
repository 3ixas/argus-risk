namespace Argus.TradeSimulator.Configuration;

/// <summary>
/// Configuration options for the trade simulator.
/// </summary>
public sealed class TradeSimulatorOptions
{
    public const string SectionName = "TradeSimulator";

    /// <summary>
    /// Target number of trades to generate per second.
    /// </summary>
    public double TradesPerSecond { get; set; } = 2.0;

    /// <summary>
    /// Random seed for reproducible trade generation.
    /// </summary>
    public int Seed { get; set; } = 123;

    /// <summary>
    /// Probability of generating a buy trade (vs sell).
    /// Default 0.6 = 60% buy, 40% sell (slight bullish bias).
    /// </summary>
    public double BuyProbability { get; set; } = 0.6;

    /// <summary>
    /// Minimum quantity per trade (shares).
    /// </summary>
    public int MinQuantity { get; set; } = 10;

    /// <summary>
    /// Maximum quantity per trade (shares).
    /// </summary>
    public int MaxQuantity { get; set; } = 500;
}
