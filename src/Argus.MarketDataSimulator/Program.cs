using Argus.Domain.Models;
using Argus.Infrastructure.Messaging;
using Argus.MarketDataSimulator.Configuration;
using Argus.MarketDataSimulator.Services;
using Argus.MarketDataSimulator.Workers;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<SimulatorOptions>(
    builder.Configuration.GetSection(SimulatorOptions.SectionName));

var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

// Services
builder.Services.AddSingleton<InstrumentRepository>();
builder.Services.AddSingleton<PriceGenerator>();
builder.Services.AddSingleton<FxRateGenerator>();

// Kafka producers
builder.Services.AddKafkaProducer<PriceTick>(kafkaBootstrapServers);
builder.Services.AddKafkaProducer<FxRate>(kafkaBootstrapServers);

// Background worker
builder.Services.AddHostedService<MarketDataWorker>();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Health endpoint for container orchestration
app.MapHealthChecks("/health");

// Simple status endpoint
app.MapGet("/", () => new
{
    Service = "Argus Market Data Simulator",
    Status = "Running",
    Timestamp = DateTimeOffset.UtcNow
});

app.Run();
