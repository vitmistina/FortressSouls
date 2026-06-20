namespace FortressSouls.Tests;

using FortressSouls.Application;
using Microsoft.AspNetCore.Mvc.Testing;

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
    public async Task HealthEndpointReturnsOkStatus()
    {
        // Arrange
        var uri = "/api/health";

        // Act
        var response = await _client!.GetAsync(uri);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
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
    }
}
