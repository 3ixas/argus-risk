namespace Argus.Infrastructure.Messaging;

/// <summary>
/// Abstraction for consuming messages from a message broker.
/// Mirrors IMessageProducer, allowing swappable implementations.
/// </summary>
public interface IMessageConsumer<TValue> : IDisposable
{
    /// <summary>
    /// Subscribes to the specified topics.
    /// Must be called before consuming messages.
    /// </summary>
    void Subscribe(params string[] topics);

    /// <summary>
    /// Consumes the next message from the subscribed topics.
    /// Blocks until a message is available or the timeout expires.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The consumed message, or null if no message available</returns>
    ConsumeResult<TValue>? Consume(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current offset, marking messages as processed.
    /// Call after successfully processing a message to prevent redelivery.
    /// </summary>
    void Commit();
}

/// <summary>
/// Result of consuming a message, containing the value and metadata.
/// </summary>
public sealed record ConsumeResult<TValue>(
    TValue Value,
    string Topic,
    int Partition,
    long Offset,
    string? Key
);
