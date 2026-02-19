using System.Diagnostics.Metrics;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.Infrastructure.Telemetry;
using Argus.RiskEngine.Caches;
using Argus.RiskEngine.Services;
using Polly;
using Polly.CircuitBreaker;

namespace Argus.RiskEngine.Workers;

/// <summary>
/// Consumes price ticks from Kafka and updates the MarketDataCache.
/// Wraps the consume-process-commit loop in a circuit breaker so that
/// consecutive Kafka failures open the circuit, degrading gracefully while
/// preserving last-known prices in cache.
/// Circuit states: 0=Closed (healthy), 1=Open (degraded), 2=HalfOpen (testing)
/// </summary>
public sealed class PriceConsumerWorker : BackgroundService
{
    private const string PricesTopic = "market-data.prices";

    // 0=closed, 1=open, 2=half-open — static so ObservableGauge callback can reference it
    private static int _circuitBreakerState;

    private static readonly Counter<long> PriceTicksConsumedCounter =
        ArgusDiagnostics.Meter.CreateCounter<long>("argus.market_data.price_ticks.consumed", description: "Total price ticks consumed");

    private static readonly ObservableGauge<int> CircuitBreakerStateGauge =
        ArgusDiagnostics.Meter.CreateObservableGauge<int>(
            "argus.circuit_breaker.state",
            () => _circuitBreakerState,
            description: "Price consumer circuit breaker state: 0=Closed, 1=Open, 2=HalfOpen");

    private readonly IMessageConsumer<PriceTick> _consumer;
    private readonly MarketDataCache _cache;
    private readonly AlertPublisher _alertPublisher;
    private readonly ILogger<PriceConsumerWorker> _logger;
    private readonly ResiliencePipeline _circuitBreaker;

    private long _ticksProcessed;
    private volatile bool _isDegraded;

    public PriceConsumerWorker(
        IMessageConsumer<PriceTick> consumer,
        MarketDataCache cache,
        AlertPublisher alertPublisher,
        ILogger<PriceConsumerWorker> logger)
    {
        _consumer = consumer;
        _cache = cache;
        _alertPublisher = alertPublisher;
        _logger = logger;

        _circuitBreaker = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                // Open after 100% failure rate across 5 attempts
                FailureRatio = 1.0,
                MinimumThroughput = 5,
                // Stay open for 30s, then half-open to test recovery
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    _isDegraded = true;
                    _circuitBreakerState = 1;
                    _logger.LogWarning(
                        "Price consumer circuit breaker OPENED - market data degraded for {Duration}s. " +
                        "Last-known prices remain in cache.",
                        args.BreakDuration.TotalSeconds);
                    _ = _alertPublisher.RaiseAsync(
                        AlertType.CircuitBreakerOpen,
                        AlertSeverity.Error,
                        "PriceConsumerWorker",
                        $"Kafka price consumer circuit breaker opened — market data feed degraded for {args.BreakDuration.TotalSeconds:F0}s");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _isDegraded = false;
                    _circuitBreakerState = 0;
                    _logger.LogInformation("Price consumer circuit breaker CLOSED - market data restored");
                    _ = _alertPublisher.ResolveAsync(AlertType.CircuitBreakerOpen, "PriceConsumerWorker");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _circuitBreakerState = 2;
                    _logger.LogInformation("Price consumer circuit breaker HALF-OPEN - testing recovery");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
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
                await _circuitBreaker.ExecuteAsync(ct =>
                {
                    var result = _consumer.Consume(ct);
                    if (result != null)
                    {
                        _cache.UpdatePrice(result.Value);
                        _ticksProcessed++;
                        PriceTicksConsumedCounter.Add(1);
                        _consumer.Commit();

                        if (_ticksProcessed % 1000 == 0)
                        {
                            _logger.LogInformation(
                                "Price consumer: {Count} ticks processed, {Instruments} instruments in cache",
                                _ticksProcessed, _cache.PriceCount);
                        }
                    }
                    return ValueTask.CompletedTask;
                }, stoppingToken);
            }
            catch (BrokenCircuitException)
            {
                // Circuit is open — last-known prices remain in cache, staleness detection will flag them.
                // Sleep briefly to avoid tight CPU loop while the circuit is open.
                await Task.Delay(1000, stoppingToken);
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
    public bool IsDegraded => _isDegraded;
}
