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
}
