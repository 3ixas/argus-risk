using System.Diagnostics;
using System.Diagnostics.Metrics;
using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.Infrastructure.Telemetry;
using Argus.RiskEngine.Services;

namespace Argus.RiskEngine.Workers;

/// <summary>
/// Background worker that consumes trades from Kafka and processes them
/// through the event sourcing pipeline. Creates a new DI scope per trade
/// so each trade gets its own IDocumentSession (unit of work).
/// </summary>
public sealed class TradeConsumerWorker : BackgroundService
{
    private const string TradesTopic = "trades.inbound";

    private static readonly Counter<long> TradesProcessedCounter =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.trades.processed", description: "Total trades processed");

    private static readonly Counter<long> TradesByDirectionCounter =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.trades.by_direction", description: "Trades by buy/sell direction");

    private static readonly Histogram<double> TradeProcessingDuration =
        ArgusDiagnostics.Meter.CreateHistogram<double>("argus.trade.processing.duration", "ms", "Trade processing latency");

    private readonly IMessageConsumer<Trade> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TradeConsumerWorker> _logger;

    private long _tradesProcessed;
    private long _buyCount;
    private long _sellCount;

    public TradeConsumerWorker(
        IMessageConsumer<Trade> consumer,
        IServiceScopeFactory scopeFactory,
        ILogger<TradeConsumerWorker> logger)
    {
        _consumer = consumer;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Trade consumer starting - subscribing to {Topic}", TradesTopic);

        _consumer.Subscribe(TradesTopic);

        // Small delay to allow consumer to join group
        await Task.Delay(1000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                if (result == null)
                {
                    continue;
                }

                var trade = result.Value;

                // Trace the full processing pipeline — linked to producer span via Kafka headers
                using var activity = ArgusDiagnostics.ActivitySource.StartActivity(
                    "trade.process",
                    ActivityKind.Consumer,
                    result.TraceContext ?? default);
                activity?.SetTag("trade.symbol", trade.Symbol);
                activity?.SetTag("trade.side", trade.Side.ToString());
                activity?.SetTag("trade.quantity", trade.Quantity);

                var sw = Stopwatch.StartNew();

                // Process trade through event sourcing pipeline (scoped session)
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var processor = scope.ServiceProvider.GetRequiredService<TradeProcessor>();
                    await processor.ProcessAsync(trade, stoppingToken);
                }

                sw.Stop();
                TradeProcessingDuration.Record(sw.Elapsed.TotalMilliseconds);

                _tradesProcessed++;
                TradesProcessedCounter.Add(1);

                var direction = trade.Side == Domain.Enums.TradeSide.Buy ? "buy" : "sell";
                TradesByDirectionCounter.Add(1, new KeyValuePair<string, object?>("direction", direction));

                if (trade.Side == Domain.Enums.TradeSide.Buy)
                    _buyCount++;
                else
                    _sellCount++;

                // Commit offset after successful processing + persistence
                _consumer.Commit();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming trade");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation(
            "Trade consumer stopping - processed {Count} trades ({Buys} buys, {Sells} sells)",
            _tradesProcessed,
            _buyCount,
            _sellCount);
    }

    public long TradesProcessed => _tradesProcessed;
    public long BuyCount => _buyCount;
    public long SellCount => _sellCount;
}
