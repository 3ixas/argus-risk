using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.MarketDataSimulator.Configuration;
using Argus.MarketDataSimulator.Services;
using Microsoft.Extensions.Options;

namespace Argus.MarketDataSimulator.Workers;

/// <summary>
/// Background service that generates and publishes market data at regular intervals.
/// Publishes price ticks to 'market-data.prices' and FX rates to 'market-data.fx'.
/// </summary>
public sealed class MarketDataWorker : BackgroundService
{
    private const string PricesTopic = "market-data.prices";
    private const string FxTopic = "market-data.fx";

    private readonly PriceGenerator _priceGenerator;
    private readonly FxRateGenerator _fxGenerator;
    private readonly IMessageProducer<PriceTick> _priceProducer;
    private readonly IMessageProducer<FxRate> _fxProducer;
    private readonly SimulatorOptions _options;
    private readonly ILogger<MarketDataWorker> _logger;

    private long _tickCount;
    private long _fxUpdateCount;

    public MarketDataWorker(
        PriceGenerator priceGenerator,
        FxRateGenerator fxGenerator,
        IMessageProducer<PriceTick> priceProducer,
        IMessageProducer<FxRate> fxProducer,
        IOptions<SimulatorOptions> options,
        ILogger<MarketDataWorker> logger)
    {
        _priceGenerator = priceGenerator;
        _fxGenerator = fxGenerator;
        _priceProducer = priceProducer;
        _fxProducer = fxProducer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Market data simulator starting. Tick interval: {IntervalMs}ms, Seed: {Seed}, Stressed: {Stressed}",
            _options.TickIntervalMs,
            _options.Seed,
            _options.StressedMode);

        // Give Kafka time to be ready
        await Task.Delay(1000, stoppingToken);

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.TickIntervalMs));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await GenerateAndPublishDataAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Market data simulator stopping gracefully");
        }
        finally
        {
            // Flush any pending messages
            _priceProducer.Flush(TimeSpan.FromSeconds(5));
            _fxProducer.Flush(TimeSpan.FromSeconds(5));
            _logger.LogInformation(
                "Market data simulator stopped. Total ticks: {TickCount}, FX updates: {FxCount}",
                _tickCount,
                _fxUpdateCount);
        }
    }

    private async Task GenerateAndPublishDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Generate and publish price ticks
            var ticks = _priceGenerator.GenerateNextTicks();
            foreach (var tick in ticks)
            {
                await _priceProducer.ProduceAsync(
                    PricesTopic,
                    tick.Symbol, // Use symbol as partition key for ordering
                    tick,
                    cancellationToken);
            }
            _tickCount += ticks.Count;

            // Generate and publish FX rates (less frequently than prices)
            // FX every 10th tick to reduce volume
            if (_tickCount % (ticks.Count * 10) == 0)
            {
                var rates = _fxGenerator.GenerateNextRates();
                foreach (var rate in rates)
                {
                    var key = $"{rate.BaseCurrency}/{rate.QuoteCurrency}";
                    await _fxProducer.ProduceAsync(FxTopic, key, rate, cancellationToken);
                }
                _fxUpdateCount += rates.Count;
            }

            // Log progress periodically (every 1000 ticks)
            if (_tickCount % 1000 == 0)
            {
                _logger.LogInformation(
                    "Published {TickCount} price ticks, {FxCount} FX updates",
                    _tickCount,
                    _fxUpdateCount);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error generating/publishing market data");
            // Continue running - transient errors shouldn't stop the simulator
        }
    }
}
