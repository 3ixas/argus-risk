namespace Argus.MarketDataSimulator.Configuration;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    /// <summary>
    /// Interval between price ticks in milliseconds.
    /// </summary>
    public int TickIntervalMs { get; init; } = 100;

    /// <summary>
    /// Random seed for deterministic replay. Use 0 for time-based seed.
    /// </summary>
    public int Seed { get; init; } = 42;

    /// <summary>
    /// Base volatility for price movements (annualised, e.g., 0.20 = 20%).
    /// Individual instruments have sector-based multipliers.
    /// </summary>
    public double BaseVolatility { get; init; } = 0.20;

    /// <summary>
    /// Correlation strength within sectors (0 = none, 1 = perfect).
    /// </summary>
    public double SectorCorrelation { get; init; } = 0.6;

    /// <summary>
    /// Whether to use "stressed" market conditions (higher volatility).
    /// </summary>
    public bool StressedMode { get; init; } = false;

    /// <summary>
    /// Volatility multiplier during stressed mode.
    /// </summary>
    public double StressMultiplier { get; init; } = 2.5;
}
