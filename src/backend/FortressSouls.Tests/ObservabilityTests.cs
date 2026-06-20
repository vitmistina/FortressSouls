namespace FortressSouls.Tests;

using System.Diagnostics.Metrics;
using FortressSouls.Observability;
using Microsoft.Extensions.Configuration;

public class ObservabilityTests
{
    [Fact]
    public void ObservabilityHealthStateDoesNotExposeEndpointOrSecrets()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
                ["OTEL_EXPORTER_OTLP_HEADERS"] = "Authorization=secret"
            })
            .Build();

        // Act
        var healthState = ObservabilityConfiguration.GetHealthState(configuration);

        // Assert
        Assert.Equal(FortressSoulsTelemetry.OtlpConfiguredObservabilityState, healthState);
        Assert.DoesNotContain("http://localhost:4317", healthState, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", healthState, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartupMetricRecordsBoundedObservabilityTag()
    {
        // Arrange
        var measurements = new List<(long Measurement, IReadOnlyList<KeyValuePair<string, object?>> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == FortressSoulsTelemetry.MeterName
                && instrument.Name == FortressSoulsTelemetry.StartupCounterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            measurements.Add((measurement, tags.ToArray()));
        });

        listener.Start();

        // Act
        FortressSoulsTelemetry.RecordStartup(FortressSoulsTelemetry.ConsoleFallbackObservabilityState);

        // Assert
        Assert.Single(measurements);
        Assert.Equal(1, measurements[0].Measurement);
        var tag = Assert.Single(measurements[0].Tags);
        Assert.Equal(FortressSoulsTelemetry.ObservabilityStateTagName, tag.Key);
        Assert.Equal(FortressSoulsTelemetry.ConsoleFallbackObservabilityState, tag.Value);
    }
}
