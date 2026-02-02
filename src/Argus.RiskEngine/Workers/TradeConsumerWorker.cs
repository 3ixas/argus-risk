using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;

namespace Argus.RiskEngine.Workers;

/// <summary>
/// Background worker that consumes trades from Kafka.
/// In Feature 2a, we just log them. Event sourcing comes in 2b.
/// </summary>
public sealed class TradeConsumerWorker : BackgroundService
{
    private const string TradesTopic = "trades.inbound";

    private readonly IMessageConsumer<Trade> _consumer;
    private readonly ILogger<TradeConsumerWorker> _logger;

    private long _tradesProcessed;
    private long _buyCount;
    private long _sellCount;

    public TradeConsumerWorker(
        IMessageConsumer<Trade> consumer,
        ILogger<TradeConsumerWorker> logger)
    {
        _consumer = consumer;
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
                _tradesProcessed++;

                if (trade.Side == Domain.Enums.TradeSide.Buy)
                    _buyCount++;
                else
                    _sellCount++;

                _logger.LogInformation(
                    "Trade received: {Side} {Qty} {Symbol} @ {Price:F2} {Ccy} [partition={Partition}, offset={Offset}]",
                    trade.Side,
                    trade.Quantity,
                    trade.Symbol,
                    trade.Price,
                    trade.Currency,
                    result.Partition,
                    result.Offset);

                // Commit offset after successful processing
                _consumer.Commit();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming trade");
                await Task.Delay(1000, stoppingToken); // Back off on error
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
