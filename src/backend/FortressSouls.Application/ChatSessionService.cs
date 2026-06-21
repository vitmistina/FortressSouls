namespace FortressSouls.Application;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using FortressSouls.Domain;
using FortressSouls.Observability;
using FortressSouls.Prompting;

public sealed class ChatSessionService(
    DwarfQueryService dwarfQueryService,
    IChatSessionStore sessionStore,
    IChatProvider chatProvider,
    PromptAssembler promptAssembler,
    ChatSessionOptions options)
{
    private readonly DwarfQueryService _dwarfQueryService = dwarfQueryService ?? throw new ArgumentNullException(nameof(dwarfQueryService));
    private readonly IChatSessionStore _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    private readonly IChatProvider _chatProvider = chatProvider ?? throw new ArgumentNullException(nameof(chatProvider));
    private readonly PromptAssembler _promptAssembler = promptAssembler ?? throw new ArgumentNullException(nameof(promptAssembler));
    private readonly ChatSessionOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<ChatSessionCreateResult> CreateSessionAsync(string dwarfId, CancellationToken cancellationToken)
    {
        if (!TryParseDwarfId(dwarfId, out var parsedDwarfId))
        {
            throw new ChatValidationException("invalid_dwarf_id", "The provided dwarf ID is invalid.");
        }

        var listResult = await _dwarfQueryService.ListDwarvesAsync(cancellationToken);
        var listed = listResult.List.Items.Any(item => item.Id == parsedDwarfId);
        if (!listed)
        {
            throw new DwarfNotFoundException(parsedDwarfId);
        }

        var snapshotResult = await _dwarfQueryService.GetDwarfSnapshotAsync(parsedDwarfId, cancellationToken);
        if (snapshotResult.Snapshot.RequestedDwarfId != parsedDwarfId
            || snapshotResult.Snapshot.Identity.Id != parsedDwarfId)
        {
            throw new DwarfFortressDataException(
                DwarfFortressDataErrorCode.InconsistentData,
                "The dwarf snapshot identity does not match the requested dwarf ID.");
        }

        var session = _sessionStore.CreateSession(snapshotResult.Snapshot);
        return new ChatSessionCreateResult(session.SessionId, session.DwarfId.ToString());
    }

    public async Task<ChatSendMessageResult> SendMessageAsync(
        string sessionId,
        string message,
        CancellationToken cancellationToken)
    {
        var parsedSessionId = ParseSessionId(sessionId);
        if (!_sessionStore.TryGetSession(parsedSessionId, out var session) || session is null)
        {
            throw new ChatSessionNotFoundException(parsedSessionId);
        }

        var normalizedMessage = NormalizeMessage(message);
        using var activity = FortressSoulsTelemetry.ActivitySource.StartActivity(
            FortressSoulsTelemetry.ChatTurnActivityName,
            ActivityKind.Internal);
        activity?.SetTag(FortressSoulsTelemetry.ChatSessionIdTagName, session.SessionId);
        activity?.SetTag(FortressSoulsTelemetry.DwarfIdTagName, session.DwarfId.ToString());
        activity?.SetTag(FortressSoulsTelemetry.SnapshotSchemaVersionTagName, session.Snapshot.SchemaVersion);

        var turnLockTaken = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            turnLockTaken = await session.TurnLock.WaitAsync(0, cancellationToken);
            if (!turnLockTaken)
            {
                activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
                throw new ChatTurnInProgressException(session.SessionId);
            }

            EnsureSnapshotIdentity(session);

            var promptResult = _promptAssembler.Assemble(
                new PromptInputs(
                    Snapshot: session.Snapshot,
                    Conversation: [.. session.Messages.Select(MapPromptConversationMessage)],
                    PlayerMessage: normalizedMessage),
                _options.PromptAssembly);

            if (!promptResult.Succeeded || string.IsNullOrEmpty(promptResult.PromptText))
            {
                activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
                throw new ChatValidationException("prompt_assembly_failed", "Failed to assemble a valid chat prompt.");
            }

            var promptId = CreatePromptId(promptResult.PromptText);
            var providerResponse = await _chatProvider.SendAsync(
                new ChatProviderRequest(promptResult.PromptText, _options.MaxAssistantMessageCharacters),
                cancellationToken);

            var assistantMessage = NormalizeAssistantMessage(providerResponse.MessageText);
            AppendSuccessfulTurn(session, normalizedMessage, assistantMessage, promptResult.PromptText);

            activity?.SetTag(FortressSoulsTelemetry.ProviderTypeTagName, providerResponse.ProviderType);
            activity?.SetTag(FortressSoulsTelemetry.LlmModelTagName, providerResponse.Model);
            activity?.SetTag(FortressSoulsTelemetry.PromptTemplateVersionTagName, promptResult.Diagnostics.TemplateVersion);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.SuccessOutcome);

            return new ChatSendMessageResult(
                SessionId: session.SessionId,
                DwarfId: session.DwarfId.ToString(),
                AssistantMessage: assistantMessage,
                Diagnostics: new ChatTurnDiagnostics(
                    Provider: providerResponse.ProviderType,
                    Model: providerResponse.Model,
                    DurationMs: Math.Max(0, (int)Math.Round(providerResponse.Duration.TotalMilliseconds, MidpointRounding.AwayFromZero)),
                    PromptId: promptId));
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.CancelledOutcome);
            throw;
        }
        catch (Exception exception) when (exception is ChatProviderException or ChatValidationException or ChatSessionNotFoundException or ChatTurnInProgressException)
        {
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
            throw;
        }
        finally
        {
            if (turnLockTaken)
            {
                session.TurnLock.Release();
            }
        }
    }

    public ChatPromptPreviewResult GetPromptPreview(string sessionId)
    {
        var parsedSessionId = ParseSessionId(sessionId);
        if (!_sessionStore.TryGetSession(parsedSessionId, out var session) || session is null)
        {
            throw new ChatSessionNotFoundException(parsedSessionId);
        }

        if (string.IsNullOrEmpty(session.LastPromptPreview))
        {
            throw new ChatValidationException("prompt_preview_unavailable", "A prompt preview is not available for this session yet.");
        }

        return new ChatPromptPreviewResult(session.SessionId, session.DwarfId.ToString(), session.LastPromptPreview);
    }

    private static void EnsureSnapshotIdentity(ChatSessionState session)
    {
        if (session.Snapshot.RequestedDwarfId != session.DwarfId || session.Snapshot.Identity.Id != session.DwarfId)
        {
            throw new ChatValidationException("chat_identity_mismatch", "The chat session dwarf identity is invalid.");
        }
    }

    private void AppendSuccessfulTurn(ChatSessionState session, string playerMessage, string assistantMessage, string promptPreview)
    {
        session.Messages.Add(new ChatHistoryMessage(ChatRole.Player, playerMessage));
        session.Messages.Add(new ChatHistoryMessage(ChatRole.Assistant, assistantMessage));

        if (session.Messages.Count > _options.MaxHistoryMessages)
        {
            var removeCount = session.Messages.Count - _options.MaxHistoryMessages;
            session.Messages.RemoveRange(0, removeCount);
        }

        session.LastPromptPreview = promptPreview;
    }

    private static PromptConversationMessage MapPromptConversationMessage(ChatHistoryMessage message) =>
        message.Role switch
        {
            ChatRole.Player => new PromptConversationMessage(PromptMessageRole.Player, message.Text),
            ChatRole.Assistant => new PromptConversationMessage(PromptMessageRole.Assistant, message.Text),
            _ => throw new ChatValidationException("chat_role_invalid", "The chat message role is invalid.")
        };

    private string NormalizeMessage(string message)
    {
        if (message is null)
        {
            throw new ChatValidationException("invalid_message", "The chat message is required.");
        }

        var normalized = message.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ChatValidationException("invalid_message", "The chat message is required.");
        }

        if (normalized.Length > _options.MaxPlayerMessageCharacters)
        {
            throw new ChatValidationException("message_too_long", "The chat message exceeds the maximum allowed length.");
        }

        return normalized;
    }

    private string NormalizeAssistantMessage(string assistantMessage)
    {
        if (assistantMessage is null)
        {
            throw new ChatProviderException(ChatProviderErrorCode.InvalidResponse, "The chat provider returned an invalid response.");
        }

        var normalized = assistantMessage.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        if (normalized.Length == 0)
        {
            throw new ChatProviderException(ChatProviderErrorCode.InvalidResponse, "The chat provider returned an empty response.");
        }

        return normalized.Length > _options.MaxAssistantMessageCharacters
            ? normalized[.._options.MaxAssistantMessageCharacters]
            : normalized;
    }

    private static bool TryParseDwarfId(string value, out DwarfId dwarfId)
    {
        try
        {
            dwarfId = DwarfId.Parse(value);
            return true;
        }
        catch (ArgumentException)
        {
            dwarfId = default;
            return false;
        }
    }

    private static string ParseSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)
            || sessionId.Length != 13
            || !sessionId.StartsWith("chat-", StringComparison.Ordinal))
        {
            throw new ChatValidationException("invalid_session_id", "The provided chat session ID is invalid.");
        }

        for (var index = 5; index < sessionId.Length; index++)
        {
            if (!char.IsAsciiDigit(sessionId[index]))
            {
                throw new ChatValidationException("invalid_session_id", "The provided chat session ID is invalid.");
            }
        }

        return sessionId;
    }

    private static string CreatePromptId(string promptText)
    {
        var bytes = Encoding.UTF8.GetBytes(promptText);
        var hash = SHA256.HashData(bytes);
        return $"prompt-{Convert.ToHexString(hash[..6]).ToLowerInvariant()}";
    }
}
