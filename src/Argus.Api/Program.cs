using Argus.Api.Caches;
using Argus.Api.Endpoints;
using Argus.Api.Hubs;
using Argus.Api.Replay;
using Argus.Api.Services;
using Argus.Api.Workers;
using Argus.Domain.Models;
using Argus.Infrastructure.Data;
using Argus.Infrastructure.EventStore;
using Argus.Infrastructure.Messaging;
using Argus.Infrastructure.Telemetry;

using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry (metrics → Prometheus, traces → Jaeger)
builder.Services.AddArgusOpenTelemetry("argus-api", builder.Configuration);

// Serialize enums as strings ("USD", "Technology") instead of integers (0, 1, 2)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Kafka configuration
var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:19092";

// Kafka consumer for risk snapshots (separate consumer group from RiskEngine)
builder.Services.AddKafkaConsumer<RiskSnapshot>(kafkaBootstrapServers, "argus-api-snapshots");

// Marten event store — shared PostgreSQL with RiskEngine (read-only for API)
var connectionString = builder.Configuration["PostgreSQL:ConnectionString"]
    ?? "Host=localhost;Database=argus;Username=argus;Password=argus";
builder.Services.AddMartenEventStore(connectionString);

// Singletons
builder.Services.AddSingleton<RiskSnapshotCache>();
builder.Services.AddSingleton<ReconciliationCache>();
builder.Services.AddSingleton<InstrumentRepository>();
builder.Services.AddSingleton<ReplaySession>();

// Scoped services
builder.Services.AddScoped<ReconciliationService>();
builder.Services.AddScoped<ReplayService>();

// SignalR with camelCase JSON (matches Kafka serialization convention)
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// CORS — required for SignalR WebSocket negotiation from browser clients
// Docker sets CORS__Origins env var; defaults to localhost:3000 for local dev
var corsOrigins = builder.Configuration["CORS:Origins"] ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins.Split(','))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Background workers
builder.Services.AddHostedService<RiskSnapshotConsumerWorker>();
builder.Services.AddHostedService<ReplayWorker>();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Observable gauge for SignalR connections (reads static counter from RiskHub)
ArgusDiagnostics.Meter.CreateObservableGauge(
    "argus.signalr.connections.active",
    () => RiskHub.ActiveConnections,
    description: "Number of active SignalR connections");

app.UseCors();

// Prometheus metrics scraping endpoint
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Health endpoint
app.MapHealthChecks("/health");

// Status endpoint
app.MapGet("/", (RiskSnapshotCache snapshotCache) => new
{
    Service = "Argus API",
    Status = "Running",
    Risk = new
    {
        HasSnapshot = snapshotCache.Latest is not null,
        PositionCount = snapshotCache.Latest?.OpenPositionCount ?? 0,
        NetPnlUsd = snapshotCache.Latest?.TotalNetPnlUsd ?? 0m
    },
    Timestamp = DateTimeOffset.UtcNow
});

// REST endpoints
app.MapPositionEndpoints();
app.MapRiskEndpoints();
app.MapInstrumentEndpoints();
app.MapReconciliationEndpoints();
app.MapReplayEndpoints();
app.MapSnapshotEndpoints();

// SignalR hub
app.MapHub<RiskHub>("/hubs/risk");

app.Run();
