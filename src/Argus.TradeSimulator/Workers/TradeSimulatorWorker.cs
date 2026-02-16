using System.Diagnostics.Metrics;
using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.Infrastructure.Telemetry;
using Argus.TradeSimulator.Configuration;
using Argus.TradeSimulator.Services;
using Microsoft.Extensions.Options;

namespace Argus.TradeSimulator.Workers;

/// <summary>
/// Background worker that generates and publishes trades to Kafka.
/// </summary>
public sealed class TradeSimulatorWorker : BackgroundService
{
    private const string TradesTopic = "trades.inbound";

    private static readonly Counter<long> TradesGeneratedCounter =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.trades.generated", description: "Total trades generated");

    private readonly TradeGenerator _tradeGenerator;
    private readonly IMessageProducer<Trade> _producer;
    private readonly TradeSimulatorOptions _options;
    private readonly ILogger<TradeSimulatorWorker> _logger;

    private long _tradesGenerated;

    public TradeSimulatorWorker(
        TradeGenerator tradeGenerator,
        IMessageProducer<Trade> producer,
        IOptions<TradeSimulatorOptions> options,
        ILogger<TradeSimulatorWorker> logger)
    {
        _tradeGenerator = tradeGenerator;
        _producer = producer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Trade simulator starting - {TradesPerSecond} trades/sec",
            _options.TradesPerSecond);

        var intervalMs = (int)(1000.0 / _options.TradesPerSecond);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var activity = ArgusDiagnostics.ActivitySource.StartActivity("trade.generate");

                var trade = _tradeGenerator.GenerateTrade();

                activity?.SetTag("trade.symbol", trade.Symbol);
                activity?.SetTag("trade.side", trade.Side.ToString());

                await _producer.ProduceAsync(
                    TradesTopic,
                    trade.InstrumentId.ToString(), // Partition by instrument for ordering
                    trade,
                    stoppingToken);

                _tradesGenerated++;
                TradesGeneratedCounter.Add(1);

                _logger.LogInformation(
                    "Trade #{Count}: {Side} {Qty} {Symbol} @ {Price:F2} {Ccy}",
                    _tradesGenerated,
                    trade.Side,
                    trade.Quantity,
                    trade.Symbol,
                    trade.Price,
                    trade.Currency);

                await Task.Delay(intervalMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating trade");
                await Task.Delay(1000, stoppingToken); // Back off on error
            }
        }

        _logger.LogInformation("Trade simulator stopping - generated {Count} trades", _tradesGenerated);
        _producer.Flush(TimeSpan.FromSeconds(5));
    }

    public long TradesGenerated => _tradesGenerated;
}
