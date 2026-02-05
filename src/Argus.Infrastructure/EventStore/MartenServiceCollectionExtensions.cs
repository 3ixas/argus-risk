using Argus.Domain.Aggregates;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Weasel.Core;

namespace Argus.Infrastructure.EventStore;

public static class MartenServiceCollectionExtensions
{
    /// <summary>
    /// Registers Marten event store with PostgreSQL.
    /// Configures inline snapshot projection for Position aggregate.
    /// </summary>
    public static IServiceCollection AddMartenEventStore(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddMarten(options =>
        {
            options.Connection(connectionString);

            // Auto-create schema in development
            options.AutoCreateSchemaObjects = AutoCreate.All;

            // Inline projection: Position updated in same transaction as event append
            options.Projections.Snapshot<Position>(SnapshotLifecycle.Inline);
        });

        return services;
    }
}
