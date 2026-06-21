namespace FortressSouls.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using FortressSouls.Application;
using FortressSouls.Llm;
using FortressSouls.Observability;

public sealed class OpenAiCompatibleChatProviderTests
{
    [Fact]
    public async Task OpenAiCompatible_SendAsync_UsesChatCompletionsShape()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"choices":[{"message":{"content":"Stone stands."}}]}""", Encoding.UTF8, "application/json")
        });
        var provider = CreateProvider(handler, new LlmProviderOptions
        {
            ProviderType = LlmProviderType.OpenAiCompatible,
            Endpoint = "https://openrouter.ai/api/v1",
            Model = "deepseek/deepseek-v3.2",
            ApiKey = "test-key",
            MaxOutputTokens = 500,
            Temperature = 0.85,
            TimeoutSeconds = 5
        });

        var result = await provider.SendAsync(new ChatProviderRequest("Prompt text", 300), CancellationToken.None);

        Assert.Equal("Stone stands.", result.MessageText);
        Assert.Equal("OpenAiCompatible", result.ProviderType);
        Assert.Equal("deepseek/deepseek-v3.2", result.Model);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("test-key", handler.LastRequest.Headers.Authorization?.Parameter);
        var requestBody = handler.LastRequestBody;
        Assert.NotNull(requestBody);
        Assert.Contains("\"model\":\"deepseek/deepseek-v3.2\"", requestBody!, StringComparison.Ordinal);
        Assert.Contains("\"messages\":[{\"role\":\"user\",\"content\":\"Prompt text\"}]", requestBody!, StringComparison.Ordinal);
        Assert.Contains("\"max_tokens\":500", requestBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiCompatible_SendAsync_MapsMissingApiKeyToInvalidConfiguration()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(handler, new LlmProviderOptions
        {
            ProviderType = LlmProviderType.OpenAiCompatible,
            Endpoint = "https://openrouter.ai/api/v1",
            Model = "deepseek/deepseek-v3.2",
            ApiKey = "",
            MaxOutputTokens = 500,
            Temperature = 0.85,
            TimeoutSeconds = 5
        });

        var exception = await Assert.ThrowsAsync<ChatProviderException>(() =>
            provider.SendAsync(new ChatProviderRequest("Prompt text", 300), CancellationToken.None));

        Assert.Equal(ChatProviderErrorCode.InvalidConfiguration, exception.ErrorCode);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task OpenAiCompatible_SendAsync_MapsTimeoutToStableError()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"late"}}]}""", Encoding.UTF8, "application/json")
            };
        });
        var provider = CreateProvider(handler, new LlmProviderOptions
        {
            ProviderType = LlmProviderType.OpenAiCompatible,
            Endpoint = "https://openrouter.ai/api/v1",
            Model = "deepseek/deepseek-v3.2",
            ApiKey = "test-key",
            MaxOutputTokens = 500,
            Temperature = 0.85,
            TimeoutSeconds = 5
        });

        var exception = await Assert.ThrowsAsync<ChatProviderException>(() =>
            provider.SendAsync(new ChatProviderRequest("Prompt text", 300), CancellationToken.None));

        Assert.Equal(ChatProviderErrorCode.Timeout, exception.ErrorCode);
    }

    [Fact]
    public async Task OpenAiCompatible_SendAsync_MapsTransportFailuresToUnavailableWithoutEndpointLeak()
    {
        var options = ValidOptions();
        var tracker = new ChatProviderStatusTracker(options);
        var provider = CreateProvider(
            new StubHandler((_, _) => throw new HttpRequestException("Dial failed for https://openrouter.ai/api/v1/chat/completions")),
            options,
            tracker);

        var exception = await Assert.ThrowsAsync<ChatProviderException>(() =>
            provider.SendAsync(new ChatProviderRequest("Prompt text", 300), CancellationToken.None));

        Assert.Equal(ChatProviderErrorCode.Unavailable, exception.ErrorCode);
        Assert.Equal("The chat provider is unavailable.", exception.Message);
        Assert.DoesNotContain("openrouter.ai", exception.Message, StringComparison.OrdinalIgnoreCase);

        var status = tracker.GetCurrentStatus();
        Assert.Equal("error", status.LastOutcome);
        Assert.Equal("transport_error", status.LastErrorCategory);
    }

    [Fact]
    public async Task OpenAiCompatible_SendAsync_MapsOversizedResponsesToStableError()
    {
        var oversized = new string('x', 70_000);
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(oversized, Encoding.UTF8, "application/json")
        });
        var provider = CreateProvider(handler, new LlmProviderOptions
        {
            ProviderType = LlmProviderType.OpenAiCompatible,
            Endpoint = "https://openrouter.ai/api/v1",
            Model = "deepseek/deepseek-v3.2",
            ApiKey = "test-key",
            MaxOutputTokens = 500,
            Temperature = 0.85,
            TimeoutSeconds = 5
        });

        var exception = await Assert.ThrowsAsync<ChatProviderException>(() =>
            provider.SendAsync(new ChatProviderRequest("Prompt text", 300), CancellationToken.None));

        Assert.Equal(ChatProviderErrorCode.ResponseTooLarge, exception.ErrorCode);
    }

    [Fact]
    public async Task OpenAiCompatible_SendAsync_MapsNonSuccessAndMalformedResponses()
    {
        var nonSuccessProvider = CreateProvider(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)), ValidOptions());
        var malformedProvider = CreateProvider(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"choices":[{"message":{"content":" "}}]}""", Encoding.UTF8, "application/json")
        }), ValidOptions());

        var nonSuccess = await Assert.ThrowsAsync<ChatProviderException>(() =>
            nonSuccessProvider.SendAsync(new ChatProviderRequest("Prompt text", 300), CancellationToken.None));
        var malformed = await Assert.ThrowsAsync<ChatProviderException>(() =>
            malformedProvider.SendAsync(new ChatProviderRequest("Prompt text", 300), CancellationToken.None));

        Assert.Equal(ChatProviderErrorCode.Unavailable, nonSuccess.ErrorCode);
        Assert.Equal(ChatProviderErrorCode.InvalidResponse, malformed.ErrorCode);
    }

    [Fact]
    public async Task OpenAiCompatible_SendAsync_EmitsContentSafeTelemetry()
    {
        var observed = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FortressSoulsTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => observed.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);

        var provider = CreateProvider(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"choices":[{"message":{"content":"ok"}}]}""", Encoding.UTF8, "application/json")
        }), ValidOptions());

        await provider.SendAsync(new ChatProviderRequest("SENTINEL-PROMPT-LEAK-CHECK", 300), CancellationToken.None);

        var observedSnapshot = observed.ToArray();
        var llmActivity = Assert.Single(observedSnapshot, activity => activity.DisplayName == FortressSoulsTelemetry.LlmChatActivityName);
        Assert.DoesNotContain(llmActivity.Tags, tag =>
            (tag.Value?.ToString() ?? string.Empty).Contains("SENTINEL-PROMPT-LEAK-CHECK", StringComparison.Ordinal));
        Assert.DoesNotContain(llmActivity.Tags, tag =>
            (tag.Value?.ToString() ?? string.Empty).Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(llmActivity.Tags, tag =>
            (tag.Value?.ToString() ?? string.Empty).Contains("test-key", StringComparison.Ordinal));
    }

    private static OpenAiCompatibleChatProvider CreateProvider(HttpMessageHandler handler, LlmProviderOptions options, IChatProviderStatusRecorder? statusRecorder = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = options.GetValidatedEndpointUri(),
            Timeout = Timeout.InfiniteTimeSpan
        };
        return new OpenAiCompatibleChatProvider(client, options, statusRecorder ?? new ChatProviderStatusTracker(options));
    }

    private static LlmProviderOptions ValidOptions() =>
        new()
        {
            ProviderType = LlmProviderType.OpenAiCompatible,
            Endpoint = "https://openrouter.ai/api/v1",
            Model = "deepseek/deepseek-v3.2",
            ApiKey = "test-key",
            MaxOutputTokens = 500,
            Temperature = 0.85,
            TimeoutSeconds = 5
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _callback;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> callback)
        {
            _callback = (request, _) => Task.FromResult(callback(request));
        }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
        {
            _callback = callback;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            }
            RequestCount++;
            return _callback(request, cancellationToken);
        }
    }
}
