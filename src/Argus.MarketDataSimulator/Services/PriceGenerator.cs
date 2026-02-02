using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.Infrastructure.Data;
using Argus.MarketDataSimulator.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Argus.MarketDataSimulator.Services;

/// <summary>
/// Generates realistic price movements using Geometric Brownian Motion (GBM).
/// Supports sector correlation — stocks in the same sector tend to move together.
///
/// GBM formula: dS = μS dt + σS dW
/// Where:
///   S = current price
///   μ = drift (expected return, typically ~0 for short-term simulation)
///   σ = volatility (annualised standard deviation)
///   dW = Wiener process increment (normally distributed random shock)
/// </summary>
public sealed class PriceGenerator
{
    private readonly InstrumentRepository _instruments;
    private readonly SimulatorOptions _options;
    private readonly ILogger<PriceGenerator> _logger;
    private readonly Random _random;

    // Current prices for each instrument (keyed by InstrumentId)
    private readonly Dictionary<Guid, decimal> _currentPrices = new();

    // Sector-specific volatility multipliers
    private static readonly Dictionary<Sector, double> SectorVolatility = new()
    {
        [Sector.Technology] = 1.4,           // High volatility (growth stocks)
        [Sector.Healthcare] = 1.0,           // Moderate
        [Sector.Finance] = 1.2,              // Above average
        [Sector.Energy] = 1.3,               // Commodity-linked, volatile
        [Sector.ConsumerDiscretionary] = 1.2,
        [Sector.ConsumerStaples] = 0.7,      // Defensive, low volatility
        [Sector.Industrials] = 1.0,
        [Sector.Materials] = 1.1,
        [Sector.Utilities] = 0.6,            // Very stable
        [Sector.RealEstate] = 0.9,
        [Sector.Communications] = 1.1
    };

    public PriceGenerator(
        InstrumentRepository instruments,
        IOptions<SimulatorOptions> options,
        ILogger<PriceGenerator> logger)
    {
        _instruments = instruments;
        _options = options.Value;
        _logger = logger;

        // Use seeded RNG for deterministic replay, or time-based for variety
        _random = _options.Seed != 0
            ? new Random(_options.Seed)
            : new Random();

        InitialisePrices();
    }

    private void InitialisePrices()
    {
        foreach (var instrument in _instruments.GetAll())
        {
            _currentPrices[instrument.Id] = instrument.BasePrice;
        }
        _logger.LogInformation("Initialised prices for {Count} instruments", _currentPrices.Count);
    }

    /// <summary>
    /// Generates the next set of price ticks for all instruments.
    /// Uses correlated random shocks within sectors.
    /// </summary>
    public IReadOnlyList<PriceTick> GenerateNextTicks()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var ticks = new List<PriceTick>();

        // Generate sector-level shocks (shared randomness within sectors)
        var sectorShocks = GenerateSectorShocks();

        foreach (var instrument in _instruments.GetAll())
        {
            var newPrice = CalculateNextPrice(instrument, sectorShocks);
            _currentPrices[instrument.Id] = newPrice;

            ticks.Add(new PriceTick(
                instrument.Id,
                instrument.Symbol,
                newPrice,
                instrument.Currency,
                timestamp
            ));
        }

        return ticks;
    }

    private Dictionary<Sector, double> GenerateSectorShocks()
    {
        var shocks = new Dictionary<Sector, double>();
        foreach (Sector sector in Enum.GetValues<Sector>())
        {
            shocks[sector] = GenerateNormalRandom();
        }
        return shocks;
    }

    private decimal CalculateNextPrice(Instrument instrument, Dictionary<Sector, double> sectorShocks)
    {
        var currentPrice = _currentPrices[instrument.Id];

        // Time step: convert tick interval to annualised fraction
        // Assuming 252 trading days, 6.5 hours/day, 3600 seconds/hour
        var secondsPerYear = 252.0 * 6.5 * 3600.0;
        var dt = (_options.TickIntervalMs / 1000.0) / secondsPerYear;

        // Base volatility adjusted for sector
        var sectorMultiplier = SectorVolatility.GetValueOrDefault(instrument.Sector, 1.0);
        var volatility = _options.BaseVolatility * sectorMultiplier;

        // Apply stress multiplier if in stressed mode
        if (_options.StressedMode)
        {
            volatility *= _options.StressMultiplier;
        }

        // Correlated random shock:
        // shock = correlation * sectorShock + sqrt(1 - correlation²) * idiosyncraticShock
        var sectorShock = sectorShocks[instrument.Sector];
        var idiosyncraticShock = GenerateNormalRandom();
        var correlation = _options.SectorCorrelation;
        var combinedShock = correlation * sectorShock +
                           Math.Sqrt(1 - correlation * correlation) * idiosyncraticShock;

        // GBM: newPrice = currentPrice * exp((μ - σ²/2) * dt + σ * sqrt(dt) * shock)
        // Using μ = 0 (no drift for short-term simulation)
        var drift = -0.5 * volatility * volatility * dt; // Ito correction
        var diffusion = volatility * Math.Sqrt(dt) * combinedShock;
        var priceMultiplier = Math.Exp(drift + diffusion);

        var newPrice = currentPrice * (decimal)priceMultiplier;

        // Floor at 0.01 to prevent negative/zero prices
        return Math.Max(newPrice, 0.01m);
    }

    /// <summary>
    /// Generates a standard normal random variable using Box-Muller transform.
    /// </summary>
    private double GenerateNormalRandom()
    {
        // Box-Muller transform
        var u1 = _random.NextDouble();
        var u2 = _random.NextDouble();

        // Avoid log(0)
        while (u1 == 0) u1 = _random.NextDouble();

        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>
    /// Gets the current price for an instrument.
    /// </summary>
    public decimal GetCurrentPrice(Guid instrumentId) =>
        _currentPrices.GetValueOrDefault(instrumentId);

    /// <summary>
    /// Resets all prices to their base values.
    /// </summary>
    public void Reset()
    {
        InitialisePrices();
        _logger.LogInformation("Price generator reset");
    }
}
