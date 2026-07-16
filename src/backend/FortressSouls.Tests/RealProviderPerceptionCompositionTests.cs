namespace FortressSouls.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FortressSouls.Api;
using FortressSouls.Application;
using FortressSouls.Llm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class RealProviderPerceptionCompositionTests
{
    [Fact]
    public async Task OpenAiCompatiblePerceptionTurn_SendsLookAroundToolAndReturnsSafeReceipt()
    {
        var handler = new SequenceHandler(
            """{"choices":[{"message":{"content":null,"tool_calls":[{"id":"call-look-around","function":{"name":"look_around","arguments":"{\"radius\":1}"}}]}}]}""",
            """{"choices":[{"message":{"content":"I see worked stone and several figures nearby."}}]}""");
        using var providerClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        using var factory = CreateFactory(providerClient);
        using var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync(
                "/api/chat/sessions",
                new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        Assert.NotNull(created);

        var sendResponse = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created!.SessionId}/messages",
            new SendChatMessageRequest("Look around and tell me what you see."));

        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var sent = await sendResponse.Content.ReadFromJsonAsync<SendChatMessageResponse>();
        Assert.NotNull(sent);
        Assert.Equal("I see worked stone and several figures nearby.", sent!.AssistantMessage.Text);
        Assert.Equal("OpenAiCompatible", sent.Diagnostics.Provider);
        Assert.Collection(
            sent.ToolReceipts,
            receipt =>
            {
                Assert.Equal("look_around", receipt.Tool);
                Assert.Equal("success", receipt.Outcome);
            });

        var status = await (await client.GetAsync("/api/provider/status"))
            .Content.ReadFromJsonAsync<ProviderStatusResponse>();
        Assert.NotNull(status);
        Assert.Equal("success", status!.LastOutcome);
        Assert.Null(status.LastErrorCategory);

        Assert.Equal(2, handler.RequestBodies.Count);
        using var firstRequest = JsonDocument.Parse(handler.RequestBodies[0]);
        var firstRoot = firstRequest.RootElement;
        var tool = Assert.Single(firstRoot.GetProperty("tools").EnumerateArray());
        Assert.Equal("function", tool.GetProperty("type").GetString());
        Assert.Equal("look_around", tool.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("auto", firstRoot.GetProperty("tool_choice").GetString());
        Assert.False(firstRoot.GetProperty("parallel_tool_calls").GetBoolean());
        Assert.Contains(
            firstRoot.GetProperty("messages").EnumerateArray(),
            message => message.GetProperty("role").GetString() == "system"
                && message.GetProperty("content").GetString()!.Contains("ENABLED_TOOLS: look_around", StringComparison.Ordinal));

        using var secondRequest = JsonDocument.Parse(handler.RequestBodies[1]);
        var secondMessages = secondRequest.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Contains(
            secondMessages,
            message => message.GetProperty("role").GetString() == "assistant"
                && message.GetProperty("tool_calls")[0].GetProperty("id").GetString() == "call-look-around");
        Assert.Contains(
            secondMessages,
            message => message.GetProperty("role").GetString() == "tool"
                && message.GetProperty("tool_call_id").GetString() == "call-look-around");

        var preview = await (await client.GetAsync($"/api/chat/sessions/{created.SessionId}/prompt-preview"))
            .Content.ReadFromJsonAsync<PromptPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Contains("ENABLED_TOOLS: look_around", preview!.PromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("call-look-around", preview.PromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"cells\"", preview.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiCompatiblePerceptionTurn_ProviderFailureUpdatesSafeStatusWithoutReceipt()
    {
        const string sensitiveBody = "sensitive-provider-body";
        var handler = new FixedStatusHandler(HttpStatusCode.ServiceUnavailable, sensitiveBody);
        using var providerClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        using var factory = CreateFactory(providerClient);
        using var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync(
                "/api/chat/sessions",
                new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        Assert.NotNull(created);

        var sendResponse = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created!.SessionId}/messages",
            new SendChatMessageRequest("Look around and tell me what you see."));
        var errorJson = await sendResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, sendResponse.StatusCode);
        Assert.DoesNotContain(sensitiveBody, errorJson, StringComparison.Ordinal);
        using var error = JsonDocument.Parse(errorJson);
        Assert.Equal("chat_provider_unavailable", error.RootElement.GetProperty("errorCode").GetString());

        var statusResponse = await client.GetAsync("/api/provider/status");
        var statusJson = await statusResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(sensitiveBody, statusJson, StringComparison.Ordinal);
        var status = JsonSerializer.Deserialize<ProviderStatusResponse>(statusJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(status);
        Assert.Equal("error", status!.LastOutcome);
        Assert.Equal("non_success_status", status.LastErrorCategory);
        Assert.Equal(1, handler.RequestCount);
    }

    private static WebApplicationFactory<Program> CreateFactory(HttpClient providerClient) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<HttpClient>();
                    services.RemoveAll<IChatProvider>();
                    services.RemoveAll<FakeChatProvider>();
                    services.RemoveAll<OpenAiCompatibleChatProvider>();
                    services.RemoveAll<LlmProviderOptions>();
                    services.RemoveAll<ChatProviderStatusTracker>();
                    services.RemoveAll<IChatProviderStatusReader>();
                    services.RemoveAll<IChatProviderStatusRecorder>();
                    services.RemoveAll<IDwarfAgent>();
                    services.RemoveAll<IAgentToolRegistry>();
                    services.RemoveAll<Microsoft.Extensions.AI.IChatClient>();

                    var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                    [
                        new KeyValuePair<string, string?>("FortressSouls:Llm:ProviderType", "OpenAiCompatible"),
                        new KeyValuePair<string, string?>("FortressSouls:Llm:Endpoint", "https://openrouter.ai/api/v1"),
                        new KeyValuePair<string, string?>("FortressSouls:Llm:Model", "test-model"),
                        new KeyValuePair<string, string?>("FortressSouls:Llm:ApiKey", "test-key"),
                        new KeyValuePair<string, string?>("FortressSouls:Llm:TimeoutSeconds", "5")
                    ]).Build();
                    services.AddFortressSoulsLlm(configuration);

                    services.RemoveAll<HttpClient>();
                    services.AddSingleton(providerClient);
                });
            });

    private sealed class SequenceHandler(params string[] responseBodies) : HttpMessageHandler
    {
        private readonly Queue<string> _responseBodies = new(responseBodies);

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (_responseBodies.Count == 0)
            {
                throw new InvalidOperationException("The controlled provider received an unexpected request.");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBodies.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FixedStatusHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/plain")
            });
        }
    }
}
