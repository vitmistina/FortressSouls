namespace FortressSouls.Llm;

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FortressSouls.Application;
using FortressSouls.Observability;

public sealed class OpenAiCompatibleChatProvider(
    HttpClient httpClient,
    LlmProviderOptions options,
    IChatProviderStatusRecorder statusRecorder) : IChatProvider
{
    private const string ProviderType = "OpenAiCompatible";
    private const int MaxResponseBytes = 64 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly LlmProviderOptions _options = options?.Validate() ?? throw new ArgumentNullException(nameof(options));
    private readonly IChatProviderStatusRecorder _statusRecorder = statusRecorder ?? throw new ArgumentNullException(nameof(statusRecorder));

    public async Task<ChatProviderResponse> SendAsync(ChatProviderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.PromptText) || request.MaxResponseCharacters <= 0)
        {
            throw new ChatProviderException(ChatProviderErrorCode.InvalidRequest, "The chat provider request is invalid.");
        }

        if (_options.ProviderType != LlmProviderType.OpenAiCompatible)
        {
            _statusRecorder.RecordFailure("invalid_configuration");
            throw MapToContractException(new LlmProviderException(
                LlmProviderErrorCode.InvalidConfiguration,
                "The chat provider configuration is invalid."));
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _statusRecorder.RecordFailure("missing_api_key");
            throw MapToContractException(new LlmProviderException(
                LlmProviderErrorCode.InvalidConfiguration,
                "The chat provider API key is missing."));
        }

        using var activity = FortressSoulsTelemetry.ActivitySource.StartActivity(
            FortressSoulsTelemetry.LlmChatActivityName,
            ActivityKind.Internal);
        activity?.SetTag(FortressSoulsTelemetry.ProviderTypeTagName, ProviderType);
        activity?.SetTag(FortressSoulsTelemetry.LlmModelTagName, _options.Model);

        var startedAt = Stopwatch.GetTimestamp();
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            var response = await SendRequestAsync(request, linkedSource.Token);
            var duration = Stopwatch.GetElapsedTime(startedAt);
            _statusRecorder.RecordSuccess(duration);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.SuccessOutcome);
            FortressSoulsTelemetry.RecordLlmRequestCount(ProviderType, _options.Model, FortressSoulsTelemetry.SuccessOutcome);
            FortressSoulsTelemetry.RecordLlmRequestDuration(duration.TotalMilliseconds, ProviderType, _options.Model, FortressSoulsTelemetry.SuccessOutcome);
            return response with { Duration = duration };
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            var duration = Stopwatch.GetElapsedTime(startedAt);
            _statusRecorder.RecordTimeout(duration);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmRequestCount(ProviderType, _options.Model, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmRequestDuration(duration.TotalMilliseconds, ProviderType, _options.Model, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmErrorCount(ProviderType, _options.Model, "timeout");
            throw new ChatProviderException(ChatProviderErrorCode.Timeout, "The chat provider request timed out.");
        }
        catch (OperationCanceledException)
        {
            _statusRecorder.RecordCancellation();
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.CancelledOutcome);
            FortressSoulsTelemetry.RecordLlmRequestCount(ProviderType, _options.Model, FortressSoulsTelemetry.CancelledOutcome);
            FortressSoulsTelemetry.RecordLlmRequestDuration(0, ProviderType, _options.Model, FortressSoulsTelemetry.CancelledOutcome);
            throw;
        }
        catch (HttpRequestException exception)
        {
            var duration = Stopwatch.GetElapsedTime(startedAt);
            var mapped = MapToContractException(new LlmProviderException(
                LlmProviderErrorCode.Unavailable,
                "The chat provider transport request failed.",
                exception));

            _statusRecorder.RecordFailure("transport_error", duration);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmRequestCount(ProviderType, _options.Model, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmRequestDuration(duration.TotalMilliseconds, ProviderType, _options.Model, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmErrorCount(ProviderType, _options.Model, "transport_error");
            throw mapped;
        }
        catch (LlmProviderException exception)
        {
            var duration = Stopwatch.GetElapsedTime(startedAt);
            var mapped = MapToContractException(exception);
            var mappedStatusError = MapStatusError(mapped.ErrorCode);

            _statusRecorder.RecordFailure(mappedStatusError, duration);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmRequestCount(ProviderType, _options.Model, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmRequestDuration(duration.TotalMilliseconds, ProviderType, _options.Model, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmErrorCount(ProviderType, _options.Model, mappedStatusError);
            throw mapped;
        }
    }

    private async Task<ChatProviderResponse> SendRequestAsync(ChatProviderRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(
                new OpenAiCompatibleChatRequest(
                    _options.Model,
                    [new OpenAiCompatibleMessage("user", request.PromptText)],
                    _options.Temperature,
                    _options.MaxOutputTokens),
                options: SerializerOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new LlmProviderException(LlmProviderErrorCode.Unavailable, "The chat provider returned a non-success response.");
        }

        var body = await ReadResponseBodyAsync(response, cancellationToken);
        OpenAiCompatibleChatResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenAiCompatibleChatResponse>(body, SerializerOptions);
        }
        catch (JsonException)
        {
            throw new LlmProviderException(LlmProviderErrorCode.InvalidResponse, "The chat provider response was malformed.");
        }

        var message = parsed?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new LlmProviderException(LlmProviderErrorCode.InvalidResponse, "The chat provider response was empty.");
        }

        return new ChatProviderResponse(message, ProviderType, _options.Model, TimeSpan.Zero);
    }

    private static async Task<byte[]> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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

        return memory.ToArray();
    }

    private static string MapStatusError(ChatProviderErrorCode errorCode) =>
        errorCode switch
        {
            ChatProviderErrorCode.InvalidConfiguration => "invalid_configuration",
            ChatProviderErrorCode.Unavailable => "non_success_status",
            ChatProviderErrorCode.ResponseTooLarge => "response_too_large",
            ChatProviderErrorCode.InvalidResponse => "invalid_response",
            ChatProviderErrorCode.Timeout => "timeout",
            _ => "provider_error"
        };

    private static ChatProviderException MapToContractException(LlmProviderException exception)
    {
        var (errorCode, message) = exception.ErrorCode switch
        {
            LlmProviderErrorCode.InvalidRequest => (ChatProviderErrorCode.InvalidRequest, "The chat provider request is invalid."),
            LlmProviderErrorCode.Unavailable => (ChatProviderErrorCode.Unavailable, "The chat provider is unavailable."),
            LlmProviderErrorCode.InvalidResponse => (ChatProviderErrorCode.InvalidResponse, "The chat provider returned an invalid response."),
            LlmProviderErrorCode.InvalidConfiguration => (ChatProviderErrorCode.InvalidConfiguration, "The chat provider configuration is invalid."),
            LlmProviderErrorCode.Timeout => (ChatProviderErrorCode.Timeout, "The chat provider request timed out."),
            LlmProviderErrorCode.ResponseTooLarge => (ChatProviderErrorCode.ResponseTooLarge, "The chat provider returned an invalid response."),
            _ => (ChatProviderErrorCode.Unavailable, "The chat provider is unavailable.")
        };

        return new ChatProviderException(errorCode, message, exception);
    }

    private sealed record OpenAiCompatibleChatRequest(
        string Model,
        IReadOnlyList<OpenAiCompatibleMessage> Messages,
        double Temperature,
        [property: JsonPropertyName("max_tokens")]
        int Max_Tokens);

    private sealed record OpenAiCompatibleMessage(string Role, string Content);

    private sealed record OpenAiCompatibleChatResponse(IReadOnlyList<OpenAiCompatibleChoice>? Choices);

    private sealed record OpenAiCompatibleChoice(OpenAiCompatibleAssistantMessage? Message);

    private sealed record OpenAiCompatibleAssistantMessage(string? Content);
}
