namespace FortressSouls.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FortressSouls.Api;
using FortressSouls.Application;
using FortressSouls.Llm;
using FortressSouls.DwarfFortress;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Integration tests for runtime status endpoints.
/// Characterize status shape, confirm no external I/O on read, and assert
/// allowed field invariants for both provider and adapter status.
/// </summary>
public sealed class RuntimeStatusTests
{
    // ─── Provider status shape ────────────────────────────────────────────────

    [Fact]
    public async Task ProviderStatusEndpoint_ReturnsAllAllowedFieldsAndNoMore()
    {
        using var factory = CreateDefaultFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/provider/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        AssertExactPropertyNames(
            root,
            "providerType",
            "model",
            "isConfigured",
            "isReady",
            "lastOutcome",
            "lastErrorCategory",
            "lastDurationMs",
            "lastUpdatedAtUtc");
    }

    [Fact]
    public async Task ProviderStatusEndpoint_FakeProvider_ReportsConfiguredAndReadyWithNotStarted()
    {
        using var factory = CreateDefaultFactory();
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<ProviderStatusResponse>("/api/provider/status");

        Assert.NotNull(status);
        Assert.Equal("Fake", status!.ProviderType);
        Assert.Equal("fake-dwarf", status.Model);
        Assert.True(status.IsConfigured);
        Assert.True(status.IsReady);
        Assert.Equal("not_started", status.LastOutcome);
        Assert.Null(status.LastErrorCategory);
        Assert.Null(status.LastDurationMs);
        Assert.Null(status.LastUpdatedAtUtc);
    }

    [Fact]
    public async Task ProviderStatusEndpoint_AfterSuccessfulChat_ReflectsSuccessOutcome()
    {
        using var factory = CreateDefaultFactory();
        using var client = factory.CreateClient();

        // Trigger a chat message to advance the status tracker
        var created = await (await client.PostAsJsonAsync(
            "/api/chat/sessions",
            new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        Assert.NotNull(created);

        await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created!.SessionId}/messages",
            new SendChatMessageRequest("Hello."));

        var status = await client.GetFromJsonAsync<ProviderStatusResponse>("/api/provider/status");

        Assert.NotNull(status);
        Assert.Equal("success", status!.LastOutcome);
        Assert.Null(status.LastErrorCategory);
        Assert.NotNull(status.LastDurationMs);
        Assert.NotNull(status.LastUpdatedAtUtc);
    }

    [Fact]
    public async Task ProviderStatusEndpoint_DirectTrackerCancellation_DoesNotMapToErrorOutcome()
    {
        var statusTracker = new ChatProviderStatusTracker(
            new LlmProviderOptions { ProviderType = LlmProviderType.Fake });

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IChatProviderStatusReader>();
                    services.RemoveAll<IChatProviderStatusRecorder>();
                    services.RemoveAll<ChatProviderStatusTracker>();
                    services.AddSingleton(statusTracker);
                    services.AddSingleton<IChatProviderStatusReader>(statusTracker);
                    services.AddSingleton<IChatProviderStatusRecorder>(statusTracker);
                });
            });

        statusTracker.RecordCancellation();

        using var client = factory.CreateClient();
        var status = await client.GetFromJsonAsync<ProviderStatusResponse>("/api/provider/status");

        Assert.NotNull(status);
        Assert.Equal("cancelled", status!.LastOutcome);
        Assert.Equal("cancelled", status.LastErrorCategory);
        Assert.True(status.IsReady, "Cancellation should not mark provider as not-ready");
    }

    [Fact]
    public async Task ProviderStatusEndpoint_RecordedFailure_ReflectsErrorOutcome()
    {
        var statusTracker = new ChatProviderStatusTracker(
            new LlmProviderOptions { ProviderType = LlmProviderType.Fake });

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IChatProviderStatusReader>();
                    services.RemoveAll<IChatProviderStatusRecorder>();
                    services.RemoveAll<ChatProviderStatusTracker>();
                    services.AddSingleton(statusTracker);
                    services.AddSingleton<IChatProviderStatusReader>(statusTracker);
                    services.AddSingleton<IChatProviderStatusRecorder>(statusTracker);
                });
            });

        statusTracker.RecordFailure("invalid_response", TimeSpan.FromMilliseconds(850));

        using var client = factory.CreateClient();
        var status = await client.GetFromJsonAsync<ProviderStatusResponse>("/api/provider/status");

        Assert.NotNull(status);
        Assert.Equal("error", status!.LastOutcome);
        Assert.Equal("invalid_response", status.LastErrorCategory);
        Assert.NotNull(status.LastDurationMs);
    }

    // ─── Adapter status shape ─────────────────────────────────────────────────

    [Fact]
    public async Task AdapterStatusEndpoint_ReturnsAllAllowedFieldsAndNoMore()
    {
        using var factory = CreateDefaultFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dwarves/adapter-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        AssertExactPropertyNames(
            root,
            "adapterType",
            "isConfigured",
            "isReady",
            "lastOutcome",
            "lastErrorCategory",
            "lastDurationMs",
            "lastUpdatedAtUtc");
    }

    [Fact]
    public async Task AdapterStatusEndpoint_FakeAdapter_ReportsConfiguredAndReadyWithNotStarted()
    {
        using var factory = CreateDefaultFactory();
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<DwarfAdapterStatusResponse>("/api/dwarves/adapter-status");

        Assert.NotNull(status);
        Assert.Equal("Fake", status!.AdapterType);
        Assert.True(status.IsConfigured);
        Assert.True(status.IsReady);
        Assert.Equal("not_started", status.LastOutcome);
        Assert.Null(status.LastErrorCategory);
        Assert.Null(status.LastDurationMs);
        Assert.Null(status.LastUpdatedAtUtc);
    }

    [Fact]
    public async Task AdapterStatusEndpoint_DfHackTrackerDirectCancellation_DoesNotMarkNotReady()
    {
        var statusTracker = new DfHackAdapterStatusTracker(enabled: true);

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IDwarfAdapterStatusReader>();
                    services.RemoveAll<IDfHackAdapterStatusRecorder>();
                    services.RemoveAll<DfHackAdapterStatusTracker>();
                    services.AddSingleton(statusTracker);
                    services.AddSingleton<IDwarfAdapterStatusReader>(statusTracker);
                    services.AddSingleton<IDfHackAdapterStatusRecorder>(statusTracker);
                });
            });

        // "request_cancelled" error category should not mark the adapter as permanently not-ready
        statusTracker.RecordFailure("error", "request_cancelled", TimeSpan.FromMilliseconds(250));

        using var client = factory.CreateClient();
        var status = await client.GetFromJsonAsync<DwarfAdapterStatusResponse>("/api/dwarves/adapter-status");

        Assert.NotNull(status);
        Assert.Equal("error", status!.LastOutcome);
        Assert.Equal("request_cancelled", status.LastErrorCategory);
        Assert.True(status.IsReady, "Cancellation should not permanently mark adapter as not-ready");
    }

    [Fact]
    public async Task AdapterStatusEndpoint_RecordedSuccess_ReflectsSuccessOutcome()
    {
        var statusTracker = new DfHackAdapterStatusTracker(enabled: true);

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IDwarfAdapterStatusReader>();
                    services.RemoveAll<IDfHackAdapterStatusRecorder>();
                    services.RemoveAll<DfHackAdapterStatusTracker>();
                    services.AddSingleton(statusTracker);
                    services.AddSingleton<IDwarfAdapterStatusReader>(statusTracker);
                    services.AddSingleton<IDfHackAdapterStatusRecorder>(statusTracker);
                });
            });

        statusTracker.RecordSuccess(TimeSpan.FromMilliseconds(120));

        using var client = factory.CreateClient();
        var status = await client.GetFromJsonAsync<DwarfAdapterStatusResponse>("/api/dwarves/adapter-status");

        Assert.NotNull(status);
        Assert.Equal("success", status!.LastOutcome);
        Assert.Null(status.LastErrorCategory);
        Assert.NotNull(status.LastDurationMs);
        Assert.NotNull(status.LastUpdatedAtUtc);
    }

    // ─── Sentinel: no prohibited content in status responses ──────────────────

    [Fact]
    public async Task ProviderStatusEndpoint_WithApiKey_DoesNotExposeKeyInResponse()
    {
        const string sentinelKey = "sk-sentinel-should-not-appear-in-status-12345";

        using var factory = CreateOpenAiFactory(sentinelKey, endpoint: "https://openrouter.ai/api/v1");
        using var client = factory.CreateClient();

        var responseBody = await client.GetStringAsync("/api/provider/status");

        Assert.DoesNotContain(sentinelKey, responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderStatusEndpoint_WithEndpointUrl_DoesNotExposeUrlInResponse()
    {
        const string sentinelEndpoint = "https://sentinel-should-not-appear.openrouter.ai/api/v1";

        using var factory = CreateOpenAiFactory(apiKey: "test-key", endpoint: sentinelEndpoint);
        using var client = factory.CreateClient();

        var responseBody = await client.GetStringAsync("/api/provider/status");

        Assert.DoesNotContain(sentinelEndpoint, responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sentinel-should-not-appear", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderStatusEndpoint_WithExceptionMessage_DoesNotExposeExceptionInResponse()
    {
        var statusTracker = new ChatProviderStatusTracker(
            new LlmProviderOptions { ProviderType = LlmProviderType.Fake });

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IChatProviderStatusReader>();
                    services.RemoveAll<IChatProviderStatusRecorder>();
                    services.RemoveAll<ChatProviderStatusTracker>();
                    services.AddSingleton(statusTracker);
                    services.AddSingleton<IChatProviderStatusReader>(statusTracker);
                    services.AddSingleton<IChatProviderStatusRecorder>(statusTracker);
                });
            });

        // Simulate that a provider failure was recorded with a stable category (no exception text)
        statusTracker.RecordFailure("provider_error");

        using var client = factory.CreateClient();
        var responseBody = await client.GetStringAsync("/api/provider/status");

        // The body must only contain the stable category, not any exception message
        Assert.DoesNotContain("Exception", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdapterStatusEndpoint_DoesNotExposePathOrCommandInResponse()
    {
        var statusTracker = new DfHackAdapterStatusTracker(enabled: true);

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IDwarfAdapterStatusReader>();
                    services.RemoveAll<IDfHackAdapterStatusRecorder>();
                    services.RemoveAll<DfHackAdapterStatusTracker>();
                    services.AddSingleton(statusTracker);
                    services.AddSingleton<IDwarfAdapterStatusReader>(statusTracker);
                    services.AddSingleton<IDfHackAdapterStatusRecorder>(statusTracker);
                });
            });

        // Record a failure with a stable error category (no path/command details)
        statusTracker.RecordFailure("error", "executable_unavailable", TimeSpan.FromMilliseconds(100));

        using var client = factory.CreateClient();
        var responseBody = await client.GetStringAsync("/api/dwarves/adapter-status");

        // Paths and exception messages must not appear — "DfHackProcess" (adapterType) is allowed
        Assert.DoesNotContain("C:\\", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dfhack-run", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hack\\dfhack", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static WebApplicationFactory<Program> CreateDefaultFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    private static WebApplicationFactory<Program> CreateOpenAiFactory(string apiKey, string endpoint) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    ReconfigureLlm(services,
                    [
                        new KeyValuePair<string, string?>("FortressSouls:Llm:ProviderType", "OpenAiCompatible"),
                        new KeyValuePair<string, string?>("FortressSouls:Llm:Endpoint", endpoint),
                        new KeyValuePair<string, string?>("FortressSouls:Llm:Model", "sentinel-model"),
                        new KeyValuePair<string, string?>("FortressSouls:Llm:ApiKey", apiKey),
                        new KeyValuePair<string, string?>("FortressSouls:Llm:TimeoutSeconds", "5"),
                    ]);
                });
            });

    private static void ReconfigureLlm(
        IServiceCollection services,
        IReadOnlyCollection<KeyValuePair<string, string?>> configValues)
    {
        services.RemoveAll<HttpClient>();
        services.RemoveAll<IChatProvider>();
        services.RemoveAll<FakeChatProvider>();
        services.RemoveAll<OpenAiCompatibleChatProvider>();
        services.RemoveAll<LlmProviderOptions>();
        services.RemoveAll<ChatProviderStatusTracker>();
        services.RemoveAll<IChatProviderStatusReader>();
        services.RemoveAll<IChatProviderStatusRecorder>();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        services.AddFortressSoulsLlm(configuration);
    }

    private sealed record ApiErrorResponse(string ErrorCode, string Message);

    private static void AssertExactPropertyNames(JsonElement element, params string[] expected)
    {
        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(expected, actual);
    }
}
