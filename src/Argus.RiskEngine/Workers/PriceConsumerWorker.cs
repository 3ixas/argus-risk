using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.RiskEngine.Caches;

namespace Argus.RiskEngine.Workers;

/// <summary>
/// Consumes price ticks from Kafka and updates the MarketDataCache.
/// Follows the same pattern as TradeConsumerWorker.
/// </summary>
public sealed class PriceConsumerWorker : BackgroundService
{
    private const string PricesTopic = "market-data.prices";

    private readonly IMessageConsumer<PriceTick> _consumer;
    private readonly MarketDataCache _cache;
    private readonly ILogger<PriceConsumerWorker> _logger;

    private long _ticksProcessed;

    public PriceConsumerWorker(
        IMessageConsumer<PriceTick> consumer,
        MarketDataCache cache,
        ILogger<PriceConsumerWorker> logger)
    {
        _consumer = consumer;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Price consumer starting - subscribing to {Topic}", PricesTopic);

        _consumer.Subscribe(PricesTopic);
        await Task.Delay(1000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                if (result == null) continue;

                _cache.UpdatePrice(result.Value);
                _ticksProcessed++;
                _consumer.Commit();

                if (_ticksProcessed % 1000 == 0)
                {
                    _logger.LogInformation(
                        "Price consumer: {Count} ticks processed, {Instruments} instruments in cache",
                        _ticksProcessed, _cache.PriceCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming price tick");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Price consumer stopping - processed {Count} ticks", _ticksProcessed);
    }

    public long TicksProcessed => _ticksProcessed;
}
