using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.MarketDataSimulator.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Argus.MarketDataSimulator.Services;

/// <summary>
/// Generates FX rate updates for currency pairs.
/// All rates are quoted against USD (e.g., EUR/USD, GBP/USD).
/// </summary>
public sealed class FxRateGenerator
{
    private readonly SimulatorOptions _options;
    private readonly ILogger<FxRateGenerator> _logger;
    private readonly Random _random;

    // Current FX rates (quote currency -> rate vs USD)
    private readonly Dictionary<Currency, decimal> _currentRates;

    // Base rates (starting values)
    private static readonly Dictionary<Currency, decimal> BaseRates = new()
    {
        [Currency.USD] = 1.0000m,
        [Currency.EUR] = 1.0850m,  // EUR/USD
        [Currency.GBP] = 1.2650m,  // GBP/USD
        [Currency.JPY] = 0.0067m,  // JPY/USD (inverted from USD/JPY ~149)
        [Currency.CHF] = 1.1250m   // CHF/USD
    };

    // FX volatility is generally lower than equity volatility
    private const double FxVolatility = 0.08; // 8% annualised

    public FxRateGenerator(IOptions<SimulatorOptions> options, ILogger<FxRateGenerator> logger)
    {
        _options = options.Value;
        _logger = logger;

        _random = _options.Seed != 0
            ? new Random(_options.Seed + 1000) // Offset seed to differ from price generator
            : new Random();

        _currentRates = new Dictionary<Currency, decimal>(BaseRates);
        _logger.LogInformation("Initialised FX rates for {Count} currency pairs", _currentRates.Count - 1);
    }

    /// <summary>
    /// Generates updated FX rates for all non-USD currencies.
    /// </summary>
    public IReadOnlyList<FxRate> GenerateNextRates()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var rates = new List<FxRate>();

        foreach (var currency in _currentRates.Keys.Where(c => c != Currency.USD))
        {
            var newRate = CalculateNextRate(currency);
            _currentRates[currency] = newRate;

            rates.Add(new FxRate(
                currency,
                Currency.USD,
                newRate,
                timestamp
            ));
        }

        return rates;
    }

    private decimal CalculateNextRate(Currency currency)
    {
        var currentRate = _currentRates[currency];

        // Same GBM approach as equities, but with FX-specific volatility
        var secondsPerYear = 252.0 * 24.0 * 3600.0; // FX trades 24/5
        var dt = (_options.TickIntervalMs / 1000.0) / secondsPerYear;

        var volatility = FxVolatility;
        if (_options.StressedMode)
        {
            volatility *= _options.StressMultiplier;
        }

        var shock = GenerateNormalRandom();
        var drift = -0.5 * volatility * volatility * dt;
        var diffusion = volatility * Math.Sqrt(dt) * shock;
        var rateMultiplier = Math.Exp(drift + diffusion);

        return Math.Max(currentRate * (decimal)rateMultiplier, 0.0001m);
    }

    private double GenerateNormalRandom()
    {
        var u1 = _random.NextDouble();
        var u2 = _random.NextDouble();
        while (u1 == 0) u1 = _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>
    /// Gets the current rate for a currency vs USD.
    /// </summary>
    public decimal GetRate(Currency currency) =>
        _currentRates.GetValueOrDefault(currency, 1.0m);

    /// <summary>
    /// Converts an amount from one currency to another.
    /// </summary>
    public decimal Convert(decimal amount, Currency from, Currency to)
    {
        if (from == to) return amount;

        // Convert to USD first, then to target
        var amountInUsd = amount * _currentRates[from];
        return amountInUsd / _currentRates[to];
    }

    public void Reset()
    {
        foreach (var kvp in BaseRates)
        {
            _currentRates[kvp.Key] = kvp.Value;
        }
        _logger.LogInformation("FX rate generator reset");
    }
}
