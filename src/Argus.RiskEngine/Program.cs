using Argus.Domain.Models;
using Argus.Infrastructure.EventStore;
using Argus.Infrastructure.Messaging;
using Argus.Infrastructure.Telemetry;
using Argus.RiskEngine.Caches;
using Argus.RiskEngine.Services;
using Argus.RiskEngine.Workers;
using Marten;
using AlertPublisher = Argus.RiskEngine.Services.AlertPublisher;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry (metrics → Prometheus, traces → Jaeger)
builder.Services.AddArgusOpenTelemetry("argus-risk-engine", builder.Configuration);

// Kafka configuration
var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:19092";

// Kafka consumers — each message type gets its own consumer group
builder.Services.AddKafkaConsumer<Trade>(kafkaBootstrapServers, "argus-risk-engine");
builder.Services.AddKafkaConsumer<PriceTick>(kafkaBootstrapServers, "argus-risk-engine-prices");
builder.Services.AddKafkaConsumer<FxRate>(kafkaBootstrapServers, "argus-risk-engine-fx");

// Kafka producers
builder.Services.AddKafkaProducer<RiskSnapshot>(kafkaBootstrapServers);
builder.Services.AddKafkaProducer<Alert>(kafkaBootstrapServers);

// Marten event store (PostgreSQL)
var connectionString = builder.Configuration["PostgreSQL:ConnectionString"]
    ?? "Host=localhost;Database=argus;Username=argus;Password=argus";
builder.Services.AddMartenEventStore(connectionString);

// In-memory caches (singletons — shared across all workers)
builder.Services.AddSingleton<MarketDataCache>();
builder.Services.AddSingleton<PositionCache>();

// Alert publisher (singleton — owns deduplication window)
builder.Services.AddSingleton<AlertPublisher>();

// Trade processing (scoped — one per trade)
builder.Services.AddScoped<TradeProcessor>();

// Background workers (registered as singleton + hosted service for DI resolution in endpoints)
builder.Services.AddSingleton<TradeConsumerWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TradeConsumerWorker>());
builder.Services.AddSingleton<PriceConsumerWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PriceConsumerWorker>());
builder.Services.AddSingleton<FxRateConsumerWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FxRateConsumerWorker>());
builder.Services.AddSingleton<RiskSnapshotWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RiskSnapshotWorker>());

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Hydrate position cache from Marten on startup
using (var scope = app.Services.CreateScope())
{
    var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
    var positionCache = scope.ServiceProvider.GetRequiredService<PositionCache>();
    await positionCache.LoadFromStoreAsync(session);

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Position cache hydrated with {Count} open positions", positionCache.Count);
}

// Observable gauges — read cache sizes at scrape time
var positionCacheForGauge = app.Services.GetRequiredService<PositionCache>();
var marketDataCacheForGauge = app.Services.GetRequiredService<MarketDataCache>();

ArgusDiagnostics.Meter.CreateObservableGauge(
    "argus.positions.open",
    () => positionCacheForGauge.Count,
    description: "Number of open positions in cache");

ArgusDiagnostics.Meter.CreateObservableGauge(
    "argus.market_data.instruments_cached",
    () => marketDataCacheForGauge.PriceCount,
    description: "Number of instruments with cached prices");

ArgusDiagnostics.Meter.CreateObservableGauge(
    "argus.market_data.fx_pairs_cached",
    () => marketDataCacheForGauge.FxRateCount,
    description: "Number of FX pairs in cache");

// Prometheus metrics scraping endpoint
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Health endpoint for container orchestration
app.MapHealthChecks("/health");

// Status endpoint with processing stats
app.MapGet("/", (
    TradeConsumerWorker? tradeWorker,
    PriceConsumerWorker? priceWorker,
    FxRateConsumerWorker? fxWorker,
    RiskSnapshotWorker? snapshotWorker,
    PositionCache positionCache,
    MarketDataCache marketDataCache) => new
{
    Service = "Argus Risk Engine",
    Status = "Running",
    Trades = new
    {
        Processed = tradeWorker?.TradesProcessed ?? 0,
        BuyCount = tradeWorker?.BuyCount ?? 0,
        SellCount = tradeWorker?.SellCount ?? 0
    },
    MarketData = new
    {
        PriceTicksProcessed = priceWorker?.TicksProcessed ?? 0,
        FxRatesProcessed = fxWorker?.RatesProcessed ?? 0,
        InstrumentsInCache = marketDataCache.PriceCount,
        FxPairsInCache = marketDataCache.FxRateCount
    },
    Risk = new
    {
        OpenPositions = positionCache.Count,
        SnapshotsPublished = snapshotWorker?.SnapshotsPublished ?? 0
    },
    Timestamp = DateTimeOffset.UtcNow
});

app.Run();
