using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
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

                // Process trade through event sourcing pipeline (scoped session)
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var processor = scope.ServiceProvider.GetRequiredService<TradeProcessor>();
                    await processor.ProcessAsync(trade, stoppingToken);
                }

                _tradesProcessed++;
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
