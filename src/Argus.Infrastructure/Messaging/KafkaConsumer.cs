using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Argus.Infrastructure.Messaging;

/// <summary>
/// Kafka consumer implementation using Confluent.Kafka.
/// Deserialises JSON messages into typed objects.
/// </summary>
public sealed class KafkaConsumer<TValue> : IMessageConsumer<TValue>
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaConsumer<TValue>> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public KafkaConsumer(
        string bootstrapServers,
        string groupId,
        ILogger<KafkaConsumer<TValue>> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest, // Start from beginning if no committed offset
            EnableAutoCommit = false, // Manual commit for exactly-once semantics
            EnablePartitionEof = false,
            SessionTimeoutMs = 30000,
            HeartbeatIntervalMs = 10000,
            MaxPollIntervalMs = 300000
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka consumer error: {Reason} (Code: {Code})", error.Reason, error.Code);
            })
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                _logger.LogInformation(
                    "Partitions assigned: {Partitions}",
                    string.Join(", ", partitions.Select(p => $"{p.Topic}[{p.Partition}]")));
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                _logger.LogInformation(
                    "Partitions revoked: {Partitions}",
                    string.Join(", ", partitions.Select(p => $"{p.Topic}[{p.Partition}]")));
            })
            .Build();

        _logger.LogInformation(
            "Kafka consumer initialised for {BootstrapServers} with group {GroupId}",
            bootstrapServers,
            groupId);
    }

    public void Subscribe(params string[] topics)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _consumer.Subscribe(topics);
        _logger.LogInformation("Subscribed to topics: {Topics}", string.Join(", ", topics));
    }

    public ConsumeResult<TValue>? Consume(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var result = _consumer.Consume(cancellationToken);
            if (result == null)
            {
                return null;
            }

            var value = JsonSerializer.Deserialize<TValue>(result.Message.Value, _jsonOptions);
            if (value == null)
            {
                _logger.LogWarning(
                    "Failed to deserialise message from {Topic}[{Partition}]@{Offset}",
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value);
                return null;
            }

            _logger.LogDebug(
                "Consumed from {Topic}[{Partition}]@{Offset}: key={Key}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                result.Message.Key);

            return new ConsumeResult<TValue>(
                value,
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                result.Message.Key);
        }
        catch (ConsumeException ex)
        {
            _logger.LogError(ex, "Error consuming message: {Reason}", ex.Error.Reason);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserialising message");
            throw;
        }
    }

    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _consumer.Commit();
        _logger.LogDebug("Offset committed");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _consumer.Close();
        _consumer.Dispose();
        _logger.LogInformation("Kafka consumer disposed");
    }
}
