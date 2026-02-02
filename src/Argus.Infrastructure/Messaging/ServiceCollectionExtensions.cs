using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Argus.Infrastructure.Messaging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a Kafka producer for the specified message type.
    /// </summary>
    public static IServiceCollection AddKafkaProducer<TValue>(
        this IServiceCollection services,
        string bootstrapServers)
    {
        services.AddSingleton<IMessageProducer<TValue>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<KafkaProducer<TValue>>>();
            return new KafkaProducer<TValue>(bootstrapServers, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a Kafka consumer for the specified message type.
    /// </summary>
    public static IServiceCollection AddKafkaConsumer<TValue>(
        this IServiceCollection services,
        string bootstrapServers,
        string groupId)
    {
        services.AddSingleton<IMessageConsumer<TValue>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<KafkaConsumer<TValue>>>();
            return new KafkaConsumer<TValue>(bootstrapServers, groupId, logger);
        });

        return services;
    }
}
