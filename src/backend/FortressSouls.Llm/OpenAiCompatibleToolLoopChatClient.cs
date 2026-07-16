namespace FortressSouls.Llm;

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FortressSouls.Observability;
using Microsoft.Extensions.AI;

public sealed class OpenAiCompatibleToolLoopChatClient(
    HttpClient httpClient,
    LlmProviderOptions options,
    IChatProviderStatusRecorder? statusRecorder = null) : IChatClient
{
    private const string ProviderType = "OpenAiCompatible";
    private const int MaxResponseBytes = 64 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly LlmProviderOptions _options = options?.Validate() ?? throw new ArgumentNullException(nameof(options));
    private readonly IChatProviderStatusRecorder? _statusRecorder = statusRecorder;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FortressSoulsTelemetry.ActivitySource.StartActivity(
            FortressSoulsTelemetry.LlmChatActivityName,
            ActivityKind.Internal);
        activity?.SetTag(FortressSoulsTelemetry.ProviderTypeTagName, ProviderType);
        activity?.SetTag(FortressSoulsTelemetry.LlmModelTagName, _options.Model);

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var response = await GetResponseCoreAsync(messages, options, cancellationToken);
            var duration = Stopwatch.GetElapsedTime(startedAt);
            _statusRecorder?.RecordSuccess(duration);
            RecordRequestTelemetry(activity, duration, FortressSoulsTelemetry.SuccessOutcome);
            return response;
        }
        catch (OperationCanceledException)
        {
            _statusRecorder?.RecordCancellation();
            RecordRequestTelemetry(activity, TimeSpan.Zero, FortressSoulsTelemetry.CancelledOutcome);
            throw;
        }
        catch (HttpRequestException exception)
        {
            var duration = Stopwatch.GetElapsedTime(startedAt);
            const string errorCategory = "transport_error";
            _statusRecorder?.RecordFailure(errorCategory, duration);
            RecordRequestTelemetry(activity, duration, FortressSoulsTelemetry.ErrorOutcome, errorCategory);
            throw new LlmProviderException(
                LlmProviderErrorCode.Unavailable,
                "The chat provider transport request failed.",
                exception);
        }
        catch (LlmProviderException exception)
        {
            var duration = Stopwatch.GetElapsedTime(startedAt);
            var errorCategory = MapStatusError(exception.ErrorCode);
            if (exception.ErrorCode == LlmProviderErrorCode.Timeout)
            {
                _statusRecorder?.RecordTimeout(duration);
            }
            else
            {
                _statusRecorder?.RecordFailure(errorCategory, duration);
            }

            RecordRequestTelemetry(activity, duration, FortressSoulsTelemetry.ErrorOutcome, errorCategory);
            throw;
        }
    }

    private async Task<ChatResponse> GetResponseCoreAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (_options.ProviderType != LlmProviderType.OpenAiCompatible)
        {
            throw new LlmProviderException(LlmProviderErrorCode.InvalidConfiguration, "The chat provider configuration is invalid.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new LlmProviderException(LlmProviderErrorCode.InvalidConfiguration, "The chat provider API key is missing.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(CreateRequest(messages, options), options: SerializerOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, linkedSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new LlmProviderException(LlmProviderErrorCode.Timeout, "The chat provider request timed out.");
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new LlmProviderException(LlmProviderErrorCode.Unavailable, "The chat provider returned a non-success response.");
            }

            string body;
            try
            {
                body = await ReadResponseBodyAsync(response, linkedSource.Token);
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new LlmProviderException(LlmProviderErrorCode.Timeout, "The chat provider request timed out.");
            }

            OpenAiCompatibleChatResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<OpenAiCompatibleChatResponse>(body, SerializerOptions);
            }
            catch (JsonException exception)
            {
                throw new LlmProviderException(LlmProviderErrorCode.InvalidResponse, "The chat provider response was malformed.", exception);
            }

            return new ChatResponse(MapAssistantMessage(parsed));
        }
    }

    private void RecordRequestTelemetry(
        Activity? activity,
        TimeSpan duration,
        string outcome,
        string? errorCategory = null)
    {
        activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, outcome);
        FortressSoulsTelemetry.RecordLlmRequestCount(ProviderType, _options.Model, outcome);
        FortressSoulsTelemetry.RecordLlmRequestDuration(duration.TotalMilliseconds, ProviderType, _options.Model, outcome);
        if (errorCategory is not null)
        {
            FortressSoulsTelemetry.RecordLlmErrorCount(ProviderType, _options.Model, errorCategory);
        }
    }

    private string MapStatusError(LlmProviderErrorCode errorCode) =>
        errorCode switch
        {
            LlmProviderErrorCode.InvalidConfiguration when string.IsNullOrWhiteSpace(_options.ApiKey) => "missing_api_key",
            LlmProviderErrorCode.InvalidConfiguration => "invalid_configuration",
            LlmProviderErrorCode.Unavailable => "non_success_status",
            LlmProviderErrorCode.ResponseTooLarge => "response_too_large",
            LlmProviderErrorCode.InvalidResponse => "invalid_response",
            LlmProviderErrorCode.Timeout => "timeout",
            LlmProviderErrorCode.InvalidRequest => "invalid_request",
            _ => "provider_error"
        };

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(HttpClient))
        {
            return _httpClient;
        }

        if (serviceType == typeof(LlmProviderOptions))
        {
            return _options;
        }

        return null;
    }

    public void Dispose()
    {
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming is not required for the R2-001 probe.");

    private OpenAiCompatibleChatRequest CreateRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var chatOptions = options ?? new ChatOptions();
        var requestMessages = MapMessages(messages, chatOptions).ToArray();
        var requestTools = MapTools(chatOptions.Tools).ToArray();

        return new OpenAiCompatibleChatRequest(
            chatOptions.ModelId ?? _options.Model,
            requestMessages,
            (double?)(chatOptions.Temperature ?? _options.Temperature) ?? _options.Temperature,
            chatOptions.MaxOutputTokens ?? _options.MaxOutputTokens,
            requestTools.Length == 0 ? null : requestTools,
            requestTools.Length == 0 ? null : "auto",
            chatOptions.AllowMultipleToolCalls);
    }

    private static ChatMessage MapAssistantMessage(OpenAiCompatibleChatResponse? response)
    {
        var message = response?.Choices?.FirstOrDefault()?.Message;
        if (message is null)
        {
            throw new LlmProviderException(LlmProviderErrorCode.InvalidResponse, "The chat provider response was empty.");
        }

        var contents = new List<AIContent>();
        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            contents.Add(new TextContent(message.Content.Trim()));
        }

        foreach (var toolCall in message.ToolCalls ?? [])
        {
            var callId = toolCall.Id?.Trim();
            var functionName = toolCall.Function?.Name?.Trim();
            var rawArguments = toolCall.Function?.Arguments;
            if (string.IsNullOrWhiteSpace(callId)
                || string.IsNullOrWhiteSpace(functionName)
                || string.IsNullOrWhiteSpace(rawArguments))
            {
                throw new LlmProviderException(LlmProviderErrorCode.InvalidResponse, "The chat provider response was malformed.");
            }

            Dictionary<string, object?>? arguments;
            try
            {
                arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(rawArguments, SerializerOptions);
            }
            catch (JsonException exception)
            {
                throw new LlmProviderException(LlmProviderErrorCode.InvalidResponse, "The chat provider response was malformed.", exception);
            }

            contents.Add(new FunctionCallContent(callId, functionName, arguments ?? new Dictionary<string, object?>()));
        }

        if (contents.Count == 0)
        {
            throw new LlmProviderException(LlmProviderErrorCode.InvalidResponse, "The chat provider response was empty.");
        }

        return new ChatMessage(ChatRole.Assistant, contents);
    }

    private static IEnumerable<OpenAiCompatibleMessage> MapMessages(IEnumerable<ChatMessage> messages, ChatOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Instructions))
        {
            yield return new OpenAiCompatibleMessage("system", options.Instructions, null, null);
        }

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.Tool)
            {
                foreach (var result in message.Contents.OfType<FunctionResultContent>())
                {
                    yield return new OpenAiCompatibleMessage(
                        "tool",
                        JsonSerializer.Serialize(result.Result, SerializerOptions),
                        null,
                        result.CallId);
                }

                continue;
            }

            var text = string.IsNullOrWhiteSpace(message.Text) ? null : message.Text;
            var functionCalls = message.Contents
                .OfType<FunctionCallContent>()
                .Select(call => new OpenAiCompatibleToolCall(
                    call.CallId,
                    new OpenAiCompatibleFunctionCall(
                        call.Name,
                        JsonSerializer.Serialize(call.Arguments, SerializerOptions))))
                .ToArray();

            if (text is null && functionCalls.Length == 0)
            {
                continue;
            }

            yield return new OpenAiCompatibleMessage(
                MapRole(message.Role),
                text,
                functionCalls.Length == 0 ? null : functionCalls,
                null);
        }
    }

    private static IEnumerable<OpenAiCompatibleTool> MapTools(IList<AITool>? tools)
    {
        if (tools is null)
        {
            yield break;
        }

        foreach (var tool in tools.OfType<AIFunctionDeclaration>())
        {
            yield return new OpenAiCompatibleTool(
                "function",
                new OpenAiCompatibleFunctionDefinition(
                    tool.Name,
                    tool.Description,
                    tool.JsonSchema));
        }
    }

    private static string MapRole(ChatRole role)
    {
        if (role == ChatRole.System)
        {
            return "system";
        }

        if (role == ChatRole.Assistant)
        {
            return "assistant";
        }

        if (role == ChatRole.User)
        {
            return "user";
        }

        if (role == ChatRole.Tool)
        {
            return "tool";
        }

        throw new LlmProviderException(LlmProviderErrorCode.InvalidRequest, "The chat provider request is invalid.");
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (memory.Length + read > MaxResponseBytes)
            {
                throw new LlmProviderException(LlmProviderErrorCode.ResponseTooLarge, "The chat provider response exceeded the allowed size.");
            }

            memory.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private sealed record OpenAiCompatibleChatRequest(
        string Model,
        IReadOnlyList<OpenAiCompatibleMessage> Messages,
        double Temperature,
        [property: JsonPropertyName("max_tokens")]
        int MaxTokens,
        IReadOnlyList<OpenAiCompatibleTool>? Tools,
        [property: JsonPropertyName("tool_choice")]
        string? ToolChoice,
        [property: JsonPropertyName("parallel_tool_calls")]
        bool? ParallelToolCalls);

    private sealed record OpenAiCompatibleMessage(
        string Role,
        string? Content,
        [property: JsonPropertyName("tool_calls")]
        IReadOnlyList<OpenAiCompatibleToolCall>? ToolCalls,
        [property: JsonPropertyName("tool_call_id")]
        string? ToolCallId);

    private sealed record OpenAiCompatibleTool(
        string Type,
        OpenAiCompatibleFunctionDefinition Function);

    private sealed record OpenAiCompatibleFunctionDefinition(
        string Name,
        string? Description,
        [property: JsonPropertyName("parameters")]
        object? Parameters);

    private sealed record OpenAiCompatibleChatResponse(IReadOnlyList<OpenAiCompatibleChoice>? Choices);

    private sealed record OpenAiCompatibleChoice(OpenAiCompatibleAssistantMessage? Message);

    private sealed record OpenAiCompatibleAssistantMessage(
        string? Content,
        [property: JsonPropertyName("tool_calls")]
        IReadOnlyList<OpenAiCompatibleToolCall>? ToolCalls);

    private sealed record OpenAiCompatibleToolCall(
        string? Id,
        OpenAiCompatibleFunctionCall? Function);

    private sealed record OpenAiCompatibleFunctionCall(
        string? Name,
        string? Arguments);
}
