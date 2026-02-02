using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.Infrastructure.Data;
using Argus.TradeSimulator.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Argus.TradeSimulator.Services;

/// <summary>
/// Generates realistic trade executions for simulation.
/// Picks random instruments, quantities, and sides with configurable biases.
/// </summary>
public sealed class TradeGenerator
{
    private readonly InstrumentRepository _instruments;
    private readonly TradeSimulatorOptions _options;
    private readonly ILogger<TradeGenerator> _logger;
    private readonly Random _random;
    private readonly List<Instrument> _instrumentList;

    public TradeGenerator(
        InstrumentRepository instruments,
        IOptions<TradeSimulatorOptions> options,
        ILogger<TradeGenerator> logger)
    {
        _instruments = instruments;
        _options = options.Value;
        _logger = logger;

        _random = _options.Seed != 0
            ? new Random(_options.Seed)
            : new Random();

        _instrumentList = _instruments.GetAll().ToList();

        _logger.LogInformation(
            "Trade generator initialised with {Count} instruments, seed={Seed}",
            _instrumentList.Count,
            _options.Seed);
    }

    /// <summary>
    /// Generates a single random trade.
    /// </summary>
    public Trade GenerateTrade()
    {
        var instrument = PickRandomInstrument();
        var side = PickSide();
        var quantity = PickQuantity();
        var price = CalculateExecutionPrice(instrument, side);

        return new Trade(
            TradeId: Guid.NewGuid(),
            InstrumentId: instrument.Id,
            Symbol: instrument.Symbol,
            Side: side,
            Quantity: quantity,
            Price: price,
            Currency: instrument.Currency,
            Timestamp: DateTimeOffset.UtcNow
        );
    }

    private Instrument PickRandomInstrument()
    {
        var index = _random.Next(_instrumentList.Count);
        return _instrumentList[index];
    }

    private TradeSide PickSide()
    {
        return _random.NextDouble() < _options.BuyProbability
            ? TradeSide.Buy
            : TradeSide.Sell;
    }

    private int PickQuantity()
    {
        // Generate quantity in round lots (multiples of 10)
        var minLots = _options.MinQuantity / 10;
        var maxLots = _options.MaxQuantity / 10;
        var lots = _random.Next(minLots, maxLots + 1);
        return lots * 10;
    }

    /// <summary>
    /// Calculates execution price with a small spread from the base price.
    /// Buys execute slightly above mid, sells slightly below (simulating bid-ask spread).
    /// </summary>
    private decimal CalculateExecutionPrice(Instrument instrument, TradeSide side)
    {
        // Use base price as reference (in production, would use live market price)
        var midPrice = instrument.BasePrice;

        // Simple spread: 0.05% for liquid stocks
        var spreadBps = 5m; // basis points
        var halfSpread = midPrice * (spreadBps / 10000m);

        return side == TradeSide.Buy
            ? midPrice + halfSpread  // Buy at ask (higher)
            : midPrice - halfSpread; // Sell at bid (lower)
    }
}
