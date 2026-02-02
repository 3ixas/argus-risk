using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.RiskEngine.Workers;

var builder = WebApplication.CreateBuilder(args);

// Kafka configuration
var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:19092";
var consumerGroup = builder.Configuration["Kafka:ConsumerGroup"] ?? "argus-risk-engine";

// Kafka consumer
builder.Services.AddKafkaConsumer<Trade>(kafkaBootstrapServers, consumerGroup);

// Background worker
builder.Services.AddHostedService<TradeConsumerWorker>();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Health endpoint for container orchestration
app.MapHealthChecks("/health");

// Status endpoint with processing stats
app.MapGet("/", (TradeConsumerWorker? worker) => new
{
    Service = "Argus Risk Engine",
    Status = "Running",
    TradesProcessed = worker?.TradesProcessed ?? 0,
    BuyCount = worker?.BuyCount ?? 0,
    SellCount = worker?.SellCount ?? 0,
    Timestamp = DateTimeOffset.UtcNow
});

app.Run();
