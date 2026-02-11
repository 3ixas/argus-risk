using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Argus.Infrastructure.Messaging;

/// <summary>
/// Kafka producer implementation using Confluent.Kafka.
/// Serialises messages to JSON for interoperability.
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
            Acks = Acks.Leader, // Wait for leader acknowledgement (balance of durability/speed)
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

        var json = JsonSerializer.Serialize(value, _jsonOptions);
        var message = new Message<string, string>
        {
            Key = key ?? Guid.NewGuid().ToString(),
            Value = json
        };

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
}
