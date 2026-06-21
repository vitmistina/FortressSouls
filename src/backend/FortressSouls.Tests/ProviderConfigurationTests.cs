namespace FortressSouls.Tests;

using System.Net;
using System.Net.Http.Json;
using FortressSouls.Api;
using FortressSouls.Application;
using FortressSouls.Llm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class ProviderConfigurationTests
{
    [Fact]
    public async Task ProviderConfiguration_DefaultsToFakeProvider()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var statusResponse = await client.GetAsync("/api/provider/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<ProviderStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.NotNull(status);
        Assert.Equal("Fake", status!.ProviderType);
        Assert.Equal("fake-dwarf", status.Model);
        Assert.True(status.IsConfigured);
        Assert.True(status.IsReady);
    }

    [Fact]
    public async Task ProviderConfiguration_OpenAiWithoutKey_ReturnsSafeStatusAndErrorMapping()
    {
        using var factory = CreateFactory(configureServices: services =>
            ReconfigureLlm(
                services,
                [
                    new KeyValuePair<string, string?>("FortressSouls:Llm:ProviderType", "OpenAiCompatible"),
                    new KeyValuePair<string, string?>("FortressSouls:Llm:Endpoint", "https://openrouter.ai/api/v1"),
                    new KeyValuePair<string, string?>("FortressSouls:Llm:Model", "deepseek/deepseek-v3.2"),
                    new KeyValuePair<string, string?>("FortressSouls:Llm:ApiKey", ""),
                    new KeyValuePair<string, string?>("FortressSouls:Llm:TimeoutSeconds", "5")
                ]));
        using var client = factory.CreateClient();

        var statusResponse = await client.GetAsync("/api/provider/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<ProviderStatusResponse>();
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.NotNull(status);
        Assert.Equal("OpenAiCompatible", status!.ProviderType);
        Assert.False(status.IsConfigured);
        Assert.False(status.IsReady);
        Assert.Equal("missing_api_key", status.LastErrorCategory);

        var created = await (await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        Assert.NotNull(created);

        var send = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created!.SessionId}/messages",
            new SendChatMessageRequest("hello"));
        var error = await send.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(HttpStatusCode.InternalServerError, send.StatusCode);
        Assert.Equal("chat_provider_invalid_configuration", error!.ErrorCode);
    }

    [Fact]
    public async Task ProviderConfiguration_StatusEndpoint_DoesNotCallProviderTransport()
    {
        var countingHandler = new CountingHandler();
        using var factory = CreateFactory(configureServices: services =>
        {
            ReconfigureLlm(
                services,
                [
                    new KeyValuePair<string, string?>("FortressSouls:Llm:ProviderType", "OpenAiCompatible"),
                    new KeyValuePair<string, string?>("FortressSouls:Llm:Endpoint", "https://openrouter.ai/api/v1"),
                    new KeyValuePair<string, string?>("FortressSouls:Llm:Model", "deepseek/deepseek-v3.2"),
                    new KeyValuePair<string, string?>("FortressSouls:Llm:ApiKey", "test-key"),
                    new KeyValuePair<string, string?>("FortressSouls:Llm:TimeoutSeconds", "5")
                ]);
            services.RemoveAll<HttpClient>();
            services.AddSingleton(new HttpClient(countingHandler)
            {
                BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
                Timeout = Timeout.InfiniteTimeSpan
            });
        });
        using var client = factory.CreateClient();

        var statusResponse = await client.GetAsync("/api/provider/status");

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.Equal(0, countingHandler.RequestCount);
    }

    private static WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection>? configureServices = null) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                if (configureServices is not null)
                {
                    builder.ConfigureServices(configureServices);
                }
            });

    private static void ReconfigureLlm(IServiceCollection services, IReadOnlyCollection<KeyValuePair<string, string?>> configValues)
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

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"ok"}}]}""")
            });
        }
    }
}
