using Argus.Domain.Models;
using Argus.Infrastructure.Data;
using Argus.Infrastructure.Messaging;
using Argus.TradeSimulator.Configuration;
using Argus.TradeSimulator.Services;
using Argus.TradeSimulator.Workers;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<TradeSimulatorOptions>(
    builder.Configuration.GetSection(TradeSimulatorOptions.SectionName));

var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:19092";

// Services
builder.Services.AddSingleton<InstrumentRepository>();
builder.Services.AddSingleton<TradeGenerator>();

// Kafka producer
builder.Services.AddKafkaProducer<Trade>(kafkaBootstrapServers);

// Background worker (registered as singleton + hosted service for DI resolution in endpoints)
builder.Services.AddSingleton<TradeSimulatorWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TradeSimulatorWorker>());

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Health endpoint for container orchestration
app.MapHealthChecks("/health");

// Status endpoint
app.MapGet("/", (TradeSimulatorWorker? worker) => new
{
    Service = "Argus Trade Simulator",
    Status = "Running",
    TradesGenerated = worker?.TradesGenerated ?? 0,
    Timestamp = DateTimeOffset.UtcNow
});

app.Run();
