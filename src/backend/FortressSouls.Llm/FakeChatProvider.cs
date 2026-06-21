namespace FortressSouls.Llm;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using FortressSouls.Application;
using FortressSouls.Observability;

public sealed class FakeChatProvider(IChatProviderStatusRecorder? statusRecorder = null) : IChatProvider
{
    private const string ProviderType = "Fake";
    private const string ModelName = "fake-dwarf";
    private static readonly TimeSpan FixedDuration = TimeSpan.FromMilliseconds(25);
    private static readonly string[] Openings =
    [
        "Aye, I can speak to that.",
        "If you ask me, stone tells the truth first.",
        "Right then, here's my take.",
        "I'll keep it plain and dwarven."
    ];
    private readonly IChatProviderStatusRecorder? _statusRecorder = statusRecorder;

    public Task<ChatProviderResponse> SendAsync(ChatProviderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.PromptText) || request.MaxResponseCharacters <= 0)
        {
            throw new ChatProviderException(ChatProviderErrorCode.InvalidRequest, "The chat provider request is invalid.");
        }

        using var activity = FortressSoulsTelemetry.ActivitySource.StartActivity(
            FortressSoulsTelemetry.LlmChatActivityName,
            ActivityKind.Internal);
        activity?.SetTag(FortressSoulsTelemetry.ProviderTypeTagName, ProviderType);
        activity?.SetTag(FortressSoulsTelemetry.LlmModelTagName, ModelName);

        try
        {
            var message = BuildDeterministicMessage(request.PromptText, request.MaxResponseCharacters);
            var response = new ChatProviderResponse(message, ProviderType, ModelName, FixedDuration);
            _statusRecorder?.RecordSuccess(FixedDuration);

            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.SuccessOutcome);
            FortressSoulsTelemetry.RecordLlmRequestCount(ProviderType, ModelName, FortressSoulsTelemetry.SuccessOutcome);
            FortressSoulsTelemetry.RecordLlmRequestDuration(FixedDuration.TotalMilliseconds, ProviderType, ModelName, FortressSoulsTelemetry.SuccessOutcome);

            return Task.FromResult(response);
        }
        catch (OperationCanceledException)
        {
            _statusRecorder?.RecordCancellation();
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.CancelledOutcome);
            FortressSoulsTelemetry.RecordLlmRequestCount(ProviderType, ModelName, FortressSoulsTelemetry.CancelledOutcome);
            FortressSoulsTelemetry.RecordLlmRequestDuration(FixedDuration.TotalMilliseconds, ProviderType, ModelName, FortressSoulsTelemetry.CancelledOutcome);
            throw;
        }
        catch (Exception exception) when (exception is ChatProviderException or ArgumentException)
        {
            _statusRecorder?.RecordFailure("provider_error", FixedDuration);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmRequestCount(ProviderType, ModelName, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmRequestDuration(FixedDuration.TotalMilliseconds, ProviderType, ModelName, FortressSoulsTelemetry.ErrorOutcome);
            FortressSoulsTelemetry.RecordLlmErrorCount(ProviderType, ModelName, "provider_error");
            throw;
        }
    }

    private static string BuildDeterministicMessage(string promptText, int maxResponseCharacters)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(promptText));
        var opening = Openings[hash[0] % Openings.Length];
        var mood = hash[1] % 2 == 0 ? "Steady hands, steady words." : "I've got concerns, but I'll answer straight.";
        var signature = Convert.ToHexString(hash[..4]).ToLowerInvariant();
        var fullMessage = $"{opening} {mood} [fake:{signature}]";

        return fullMessage.Length > maxResponseCharacters
            ? fullMessage[..maxResponseCharacters]
            : fullMessage;
    }
}
