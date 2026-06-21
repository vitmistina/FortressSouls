namespace FortressSouls.Tests;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FortressSouls.Api;
using FortressSouls.Application;
using FortressSouls.Domain;
using FortressSouls.Prompting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class ChatApiTests
{
    [Fact]
    public async Task ChatLifecycle_CreateSendAndPreview_WorksInDevelopment()
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4101"));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        Assert.NotNull(created);

        var sendResponse = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created!.SessionId}/messages",
            new SendChatMessageRequest("How goes the mine?"));
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var sent = await sendResponse.Content.ReadFromJsonAsync<SendChatMessageResponse>();
        Assert.NotNull(sent);
        Assert.Equal(created.SessionId, sent!.SessionId);
        Assert.Equal("4101", sent.DwarfId);
        Assert.Equal("assistant", sent.AssistantMessage.Role);
        Assert.False(string.IsNullOrWhiteSpace(sent.AssistantMessage.Text));
        Assert.Equal("Fake", sent.Diagnostics.Provider);
        Assert.Equal("fake-dwarf", sent.Diagnostics.Model);

        var previewResponse = await client.GetAsync($"/api/chat/sessions/{created.SessionId}/prompt-preview");
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<PromptPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(created.SessionId, preview!.SessionId);
        Assert.Equal("4101", preview.DwarfId);
        Assert.Contains("PLAYER_MESSAGE_JSON:", preview.PromptText, StringComparison.Ordinal);
        Assert.Contains("How goes the mine?", preview.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PromptPreviewEndpoint_IsNotMappedOutsideDevelopment()
    {
        using var factory = CreateFactory("Production");
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4101"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        Assert.NotNull(created);

        var response = await client.GetAsync($"/api/chat/sessions/{created!.SessionId}/prompt-preview");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SessionsAreProcessLocal_AndDoNotPersistAcrossFactoryRestart()
    {
        string sessionId;
        using (var factory = CreateFactory("Development"))
        using (var client = factory.CreateClient())
        {
            var createResponse = await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4102"));
            var created = await createResponse.Content.ReadFromJsonAsync<CreateChatSessionResponse>();
            sessionId = created!.SessionId;
        }

        using var restartedFactory = CreateFactory("Development");
        using var restartedClient = restartedFactory.CreateClient();
        var sendResponse = await restartedClient.PostAsJsonAsync(
            $"/api/chat/sessions/{sessionId}/messages",
            new SendChatMessageRequest("Still there?"));

        Assert.Equal(HttpStatusCode.NotFound, sendResponse.StatusCode);
        var error = await sendResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("chat_session_not_found", error!.ErrorCode);
    }

    [Fact]
    public async Task SessionStore_AppliesDeterministicBoundsForSessionsAndHistory()
    {
        var options = new ChatSessionOptions
        {
            MaxSessions = 1,
            MaxHistoryMessages = 4,
            MaxPlayerMessageCharacters = 1_200,
            MaxAssistantMessageCharacters = 1_200,
            PromptAssembly = PromptAssemblyOptions.Default
        };

        using var factory = CreateFactory("Development", options: options);
        using var client = factory.CreateClient();

        var first = await (await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        var second = await (await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4102")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();

        var evictedSend = await client.PostAsJsonAsync($"/api/chat/sessions/{first!.SessionId}/messages", new SendChatMessageRequest("evicted?"));
        Assert.Equal(HttpStatusCode.NotFound, evictedSend.StatusCode);

        await client.PostAsJsonAsync($"/api/chat/sessions/{second!.SessionId}/messages", new SendChatMessageRequest("m1-oldest"));
        await client.PostAsJsonAsync($"/api/chat/sessions/{second.SessionId}/messages", new SendChatMessageRequest("m2-middle"));
        await client.PostAsJsonAsync($"/api/chat/sessions/{second.SessionId}/messages", new SendChatMessageRequest("m3-middle"));
        await client.PostAsJsonAsync($"/api/chat/sessions/{second.SessionId}/messages", new SendChatMessageRequest("m4-latest"));

        var preview = await (await client.GetAsync($"/api/chat/sessions/{second.SessionId}/prompt-preview"))
            .Content.ReadFromJsonAsync<PromptPreviewResponse>();

        Assert.NotNull(preview);
        Assert.DoesNotContain("m1-oldest", preview!.PromptText, StringComparison.Ordinal);
        Assert.Contains("m2-middle", preview.PromptText, StringComparison.Ordinal);
        Assert.Contains("m3-middle", preview.PromptText, StringComparison.Ordinal);
        Assert.Contains("m4-latest", preview.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionBindsDwarfIdentityAndPreventsCrossDwarfMixing()
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4103")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        await client.PostAsJsonAsync($"/api/chat/sessions/{created!.SessionId}/messages", new SendChatMessageRequest("records?"));

        var preview = await (await client.GetAsync($"/api/chat/sessions/{created.SessionId}/prompt-preview"))
            .Content.ReadFromJsonAsync<PromptPreviewResponse>();

        Assert.NotNull(preview);
        Assert.Contains("\"requestedDwarfId\":\"4103\"", preview!.PromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"requestedDwarfId\":\"4102\"", preview.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentTurns_ReturnConflictAndDoNotMutateFailedTurnHistory()
    {
        var provider = new BlockingChatProvider();
        using var factory = CreateFactory("Development", provider: provider);
        using var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();

        var firstTurn = client.PostAsJsonAsync($"/api/chat/sessions/{created!.SessionId}/messages", new SendChatMessageRequest("first-turn"));
        await provider.WaitUntilEnteredAsync();

        var secondTurn = await client.PostAsJsonAsync($"/api/chat/sessions/{created.SessionId}/messages", new SendChatMessageRequest("second-turn"));
        Assert.Equal(HttpStatusCode.Conflict, secondTurn.StatusCode);

        provider.Release();
        var firstResponse = await firstTurn;
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var preview = await (await client.GetAsync($"/api/chat/sessions/{created.SessionId}/prompt-preview"))
            .Content.ReadFromJsonAsync<PromptPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Contains("first-turn", preview!.PromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("second-turn", preview.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedOrCancelledTurn_DoesNotAppendPartialAssistantOrPlayer()
    {
        using var factory = CreateFactory("Development", provider: new FailingThenSuccessProvider());
        using var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();

        var failedResponse = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created!.SessionId}/messages",
            new SendChatMessageRequest("fail-this-message"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedResponse.StatusCode);

        var successResponse = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created.SessionId}/messages",
            new SendChatMessageRequest("good-message"));
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);

        var preview = await (await client.GetAsync($"/api/chat/sessions/{created.SessionId}/prompt-preview"))
            .Content.ReadFromJsonAsync<PromptPreviewResponse>();
        Assert.NotNull(preview);
        Assert.DoesNotContain("fail-this-message", preview!.PromptText, StringComparison.Ordinal);
        Assert.Contains("good-message", preview.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelledTurn_DoesNotAppendPlayerHistory()
    {
        using var factory = CreateFactory("Development", provider: new CancelledThenSuccessProvider());
        using var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();

        var cancelledResponse = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created!.SessionId}/messages",
            new SendChatMessageRequest("cancelled-message"));
        Assert.Equal(HttpStatusCode.RequestTimeout, cancelledResponse.StatusCode);

        var successResponse = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created.SessionId}/messages",
            new SendChatMessageRequest("survived-message"));
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);

        var preview = await (await client.GetAsync($"/api/chat/sessions/{created.SessionId}/prompt-preview"))
            .Content.ReadFromJsonAsync<PromptPreviewResponse>();
        Assert.NotNull(preview);
        Assert.DoesNotContain("cancelled-message", preview!.PromptText, StringComparison.Ordinal);
        Assert.Contains("survived-message", preview.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChatTurnTelemetry_IsNestedAndContentFree()
    {
        var observed = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FortressSouls.Observability.FortressSoulsTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => observed.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/api/chat/sessions", new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        var response = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created!.SessionId}/messages",
            new SendChatMessageRequest("SENTINEL-CONTENT-DO-NOT-LEAK"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatTurn = Assert.Single(observed, activity => activity.DisplayName == "fortresssouls.chat.turn");
        var prompt = Assert.Single(observed, activity => activity.DisplayName == "fortresssouls.prompt.assemble");
        var llm = Assert.Single(observed, activity => activity.DisplayName == "fortresssouls.llm.chat");

        Assert.Equal(chatTurn.SpanId, prompt.ParentSpanId);
        Assert.Equal(chatTurn.SpanId, llm.ParentSpanId);
        Assert.Equal("4101", chatTurn.GetTagItem("fortresssouls.dwarf.id"));
        Assert.Equal(DwarfSchemaVersions.Snapshot, chatTurn.GetTagItem("fortresssouls.snapshot.schema_version"));
        Assert.Equal("Fake", llm.GetTagItem("fortresssouls.provider.type"));
        Assert.Equal("fake-dwarf", llm.GetTagItem("fortresssouls.llm.model"));

        Assert.DoesNotContain(observed.SelectMany(activity => activity.Tags), tag =>
            (tag.Value?.ToString() ?? string.Empty).Contains("SENTINEL-CONTENT-DO-NOT-LEAK", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendMessage_WithMalformedSessionId_ReturnsBadRequest()
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/chat/sessions/not-a-session/messages",
            new SendChatMessageRequest("hello"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("invalid_session_id", error!.ErrorCode);
    }

    [Fact]
    public async Task PromptPreview_WithWhitespaceSessionId_ReturnsBadRequest()
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/chat/sessions/%20/prompt-preview");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("invalid_session_id", error!.ErrorCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string environmentName,
        IChatProvider? provider = null,
        ChatSessionOptions? options = null) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environmentName);
                builder.ConfigureServices(services =>
                {
                    if (provider is not null)
                    {
                        services.RemoveAll<IChatProvider>();
                        services.AddSingleton(provider);
                    }

                    if (options is not null)
                    {
                        services.RemoveAll<ChatSessionOptions>();
                        services.AddSingleton(options);
                    }
                });
            });

    private sealed class BlockingChatProvider : IChatProvider
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ChatProviderResponse> SendAsync(ChatProviderRequest request, CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new ChatProviderResponse("blocked-response", "BlockingFake", "blocking-model", TimeSpan.FromMilliseconds(10));
        }

        public Task WaitUntilEnteredAsync() => _entered.Task;

        public void Release() => _release.TrySetResult();
    }

    private sealed class FailingThenSuccessProvider : IChatProvider
    {
        private int _callCount;

        public Task<ChatProviderResponse> SendAsync(ChatProviderRequest request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                throw new ChatProviderException(ChatProviderErrorCode.Unavailable, "Simulated provider outage.");
            }

            return Task.FromResult(new ChatProviderResponse("recovered-response", "Fake", "fake-dwarf", TimeSpan.FromMilliseconds(25)));
        }
    }

    private sealed class CancelledThenSuccessProvider : IChatProvider
    {
        private int _callCount;

        public Task<ChatProviderResponse> SendAsync(ChatProviderRequest request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                return Task.FromCanceled<ChatProviderResponse>(new CancellationToken(canceled: true));
            }

            return Task.FromResult(new ChatProviderResponse("recovered-response", "Fake", "fake-dwarf", TimeSpan.FromMilliseconds(25)));
        }
    }

    private sealed record ApiErrorResponse(string ErrorCode, string Message);
}
