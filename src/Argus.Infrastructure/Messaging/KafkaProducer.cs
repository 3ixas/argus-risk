using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Argus.Infrastructure.Telemetry;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Argus.Infrastructure.Messaging;

/// <summary>
/// Kafka producer implementation using Confluent.Kafka.
/// Serialises messages to JSON for interoperability.
/// Injects W3C trace context into Kafka headers for distributed tracing.
/// </summary>
public sealed class KafkaProducer<TValue> : IMessageProducer<TValue>, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer<TValue>> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public KafkaProducer(string bootstrapServers, ILogger<KafkaProducer<TValue>> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All, // Required when EnableIdempotence is true
            EnableIdempotence = true, // Exactly-once semantics within a partition
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100,
            LingerMs = 5, // Small batching for throughput
            CompressionType = CompressionType.Snappy
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka producer error: {Reason} (Code: {Code})", error.Reason, error.Code);
            })
            .Build();

        _logger.LogInformation("Kafka producer initialised for {BootstrapServers}", bootstrapServers);
    }

    public async Task ProduceAsync(string topic, string? key, TValue value, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Start a span for the produce operation
        using var activity = ArgusDiagnostics.ActivitySource.StartActivity(
            $"kafka.produce {topic}", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination", topic);

        var json = JsonSerializer.Serialize(value, _jsonOptions);
        var message = new Message<string, string>
        {
            Key = key ?? Guid.NewGuid().ToString(),
            Value = json,
            Headers = new Headers()
        };

        // Inject W3C trace context into Kafka headers
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(
                activity?.Context ?? Activity.Current?.Context ?? default,
                Baggage.Current),
            message.Headers,
            InjectTraceContext);

        try
        {
            var result = await _producer.ProduceAsync(topic, message, cancellationToken);
            _logger.LogDebug(
                "Produced to {Topic}[{Partition}]@{Offset}: key={Key}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                key);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to produce message to {Topic}: {Reason}", topic, ex.Error.Reason);
            throw;
        }
    }

    public void Flush(TimeSpan timeout)
    {
        _producer.Flush(timeout);
        _logger.LogDebug("Kafka producer flushed");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _producer.Dispose();
        _logger.LogInformation("Kafka producer disposed");
    }

    /// <summary>
    /// Callback for OTel propagator to write trace context into Kafka headers.
    /// </summary>
    private static void InjectTraceContext(Headers headers, string key, string value)
    {
        headers.Add(key, Encoding.UTF8.GetBytes(value));
    }
}
