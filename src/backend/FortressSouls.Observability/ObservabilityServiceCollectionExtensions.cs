namespace FortressSouls.Observability;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddFortressSoulsObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddSingleton(FortressSoulsTelemetry.ActivitySource)
            .AddSingleton(FortressSoulsTelemetry.Meter);

        // Add OpenTelemetry with OTLP exporter if configured
        if (ObservabilityConfiguration.TryGetOtlpEndpoint(
                configuration,
                out var endpoint))
        {
            services.AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(FortressSoulsTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(options => options.Endpoint = endpoint))
                .WithMetrics(builder => builder
                    .AddMeter(FortressSoulsTelemetry.MeterName)
                    .AddOtlpExporter(options => options.Endpoint = endpoint));
        }

        return services;
    }
}
