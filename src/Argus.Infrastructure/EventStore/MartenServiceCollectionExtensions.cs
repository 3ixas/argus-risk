using Argus.Domain.Aggregates;
using Argus.Domain.Models;
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
    /// Registers RiskSnapshot as a document for historical replay.
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

            // RiskSnapshot document for historical replay queries
            options.Schema.For<RiskSnapshot>()
                .Index(x => x.Timestamp);
        });

        return services;
    }
}
