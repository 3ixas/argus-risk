namespace Argus.Infrastructure.Messaging;

/// <summary>
/// Abstraction for publishing messages to a message broker.
/// Allows swapping implementations (Kafka, RabbitMQ, in-memory for tests).
/// </summary>
public interface IMessageProducer<TValue>
{
    /// <summary>
    /// Publishes a message to the specified topic.
    /// </summary>
    /// <param name="topic">The topic/queue name to publish to</param>
    /// <param name="key">Optional partition key for ordering guarantees</param>
    /// <param name="value">The message payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ProduceAsync(string topic, string? key, TValue value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any buffered messages to ensure delivery.
    /// Call this before shutdown for graceful termination.
    /// </summary>
    void Flush(TimeSpan timeout);
}
