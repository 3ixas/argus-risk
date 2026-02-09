using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.RiskEngine.Caches;

namespace Argus.RiskEngine.Workers;

/// <summary>
/// Consumes FX rate updates from Kafka and updates the MarketDataCache.
/// Follows the same pattern as TradeConsumerWorker.
/// </summary>
public sealed class FxRateConsumerWorker : BackgroundService
{
    private const string FxTopic = "market-data.fx";

    private readonly IMessageConsumer<FxRate> _consumer;
    private readonly MarketDataCache _cache;
    private readonly ILogger<FxRateConsumerWorker> _logger;

    private long _ratesProcessed;

    public FxRateConsumerWorker(
        IMessageConsumer<FxRate> consumer,
        MarketDataCache cache,
        ILogger<FxRateConsumerWorker> logger)
    {
        _consumer = consumer;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FX rate consumer starting - subscribing to {Topic}", FxTopic);

        _consumer.Subscribe(FxTopic);
        await Task.Delay(1000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                if (result == null) continue;

                _cache.UpdateFxRate(result.Value);
                _ratesProcessed++;
                _consumer.Commit();

                if (_ratesProcessed % 100 == 0)
                {
                    _logger.LogInformation(
                        "FX consumer: {Count} rates processed, {Pairs} pairs in cache",
                        _ratesProcessed, _cache.FxRateCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming FX rate");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("FX consumer stopping - processed {Count} rates", _ratesProcessed);
    }

    public long RatesProcessed => _ratesProcessed;
}
