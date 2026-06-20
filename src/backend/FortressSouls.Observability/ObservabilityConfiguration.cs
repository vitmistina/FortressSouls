namespace FortressSouls.Observability;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

public static class ObservabilityConfiguration
{
    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static string GetHealthState(IConfiguration configuration) =>
        TryGetOtlpEndpoint(configuration, out _)
            ? FortressSoulsTelemetry.OtlpConfiguredObservabilityState
            : FortressSoulsTelemetry.ConsoleFallbackObservabilityState;

    public static bool TryGetOtlpEndpoint(IConfiguration configuration, [NotNullWhen(true)] out Uri? endpoint)
    {
        var rawEndpoint = configuration[OtlpEndpointKey];
        if (string.IsNullOrWhiteSpace(rawEndpoint))
        {
            endpoint = null;
            return false;
        }

        if (Uri.TryCreate(rawEndpoint, UriKind.Absolute, out endpoint))
        {
            return true;
        }

        endpoint = null;
        return false;
    }
}
