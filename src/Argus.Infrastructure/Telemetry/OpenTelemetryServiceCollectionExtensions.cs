using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Argus.Infrastructure.Telemetry;

public static class OpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers OpenTelemetry metrics (Prometheus) and traces (OTLP → Jaeger)
    /// with ASP.NET Core and HTTP client auto-instrumentation.
    /// </summary>
    public static IServiceCollection AddArgusOpenTelemetry(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration)
    {
        var jaegerEndpoint = configuration["Otel:JaegerEndpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(ArgusDiagnostics.ServiceName)
                .AddPrometheusExporter())
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(ArgusDiagnostics.ServiceName)
                .AddOtlpExporter(opts => opts.Endpoint = new Uri(jaegerEndpoint)));

        return services;
    }
}
