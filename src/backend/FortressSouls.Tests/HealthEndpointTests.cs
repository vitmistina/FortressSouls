namespace FortressSouls.Tests;

using FortressSouls.Application;
using FortressSouls.Observability;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.RegularExpressions;

/// <summary>
/// Integration tests for the health endpoint.
/// </summary>
public class HealthEndpointTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task HealthEndpointGeneratesCorrelationIdWhenMissing()
    {
        // Arrange
        var uri = "/api/health";

        // Act
        var response = await _client!.GetAsync(uri);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(FortressSoulsTelemetry.CorrelationHeaderName, out var values));

        var correlationId = Assert.Single(values);
        Assert.True(IsSafeCorrelationId(correlationId));
    }

    [Fact]
    public async Task HealthEndpointPreservesValidCorrelationId()
    {
        // Arrange
        var uri = "/api/health";
        var expectedCorrelationId = "trace-123_abc";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add(FortressSoulsTelemetry.CorrelationHeaderName, expectedCorrelationId);

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(FortressSoulsTelemetry.CorrelationHeaderName, out var values));
        Assert.Equal(expectedCorrelationId, Assert.Single(values));
    }

    [Fact]
    public async Task HealthEndpointReplacesInvalidCorrelationId()
    {
        // Arrange
        var uri = "/api/health";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add(FortressSoulsTelemetry.CorrelationHeaderName, new string('a', 65));

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(FortressSoulsTelemetry.CorrelationHeaderName, out var values));

        var correlationId = Assert.Single(values);
        Assert.NotEqual(new string('a', 65), correlationId);
        Assert.True(IsSafeCorrelationId(correlationId));
    }

    [Fact]
    public async Task HealthEndpointProducesTraceData()
    {
        // Arrange
        var observedActivityNames = new List<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FortressSoulsTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => observedActivityNames.Add(activity.DisplayName),
        };

        ActivitySource.AddActivityListener(listener);

        // Act
        var response = await _client!.GetAsync("/api/health");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("request", observedActivityNames);
    }

    [Fact]
    public async Task HealthEndpointReturnsExpectedContract()
    {
        // Arrange
        var uri = "/api/health";

        // Act
        var response = await _client!.GetAsync(uri);
        var content = await response.Content.ReadAsStringAsync();
        var health = System.Text.Json.JsonSerializer.Deserialize<HealthResponse>(
            content,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert
        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
        Assert.Equal("0.1.0", health.Version);
        Assert.NotNull(health.Adapter);
        Assert.NotNull(health.Provider);
        Assert.Equal(FortressSoulsTelemetry.ConsoleFallbackObservabilityState, health.Observability);
    }

    private static bool IsSafeCorrelationId(string value) =>
        value.Length is > 0 and <= 64
        && Regex.IsMatch(value, "^[A-Za-z0-9_.-]+$");
}
