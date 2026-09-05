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
    PromptAssembler promptAssembler,
    ChatSessionOptions options,
    ISurroundingsInspectionService surroundingsInspectionService,
    IEnumerable<IDwarfAgent> dwarfAgents)
{
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(30);
    private static readonly AgentExecutionPolicy NormalExecutionPolicy = new(3, 2, 2_048, 4_096, TurnTimeout, ToolTimeout);
    private static readonly PromptToolDefinition[] NormalPromptTools =
    [
        new(FakePerceptionToolService.InspectStocksToolName, PromptContract.InspectStocksArgumentsSchemaVersion, PromptContract.InspectStocksResultSchemaVersion),
        new(FakePerceptionToolService.ListDwarvesToolName, PromptContract.ListDwarvesArgumentsSchemaVersion, PromptContract.ListDwarvesResultSchemaVersion),
        new(FakePerceptionToolService.InspectDwarfToolName, PromptContract.InspectDwarfArgumentsSchemaVersion, PromptContract.InspectDwarfResultSchemaVersion)
    ];
    private static readonly string[] NormalToolNames =
    [
        FakePerceptionToolService.InspectStocksToolName,
        FakePerceptionToolService.ListDwarvesToolName,
        FakePerceptionToolService.InspectDwarfToolName
    ];

    private readonly DwarfQueryService _dwarfQueryService = dwarfQueryService ?? throw new ArgumentNullException(nameof(dwarfQueryService));
    private readonly IChatSessionStore _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    private readonly PromptAssembler _promptAssembler = promptAssembler ?? throw new ArgumentNullException(nameof(promptAssembler));
    private readonly ChatSessionOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ISurroundingsInspectionService _surroundingsInspectionService = surroundingsInspectionService ?? throw new ArgumentNullException(nameof(surroundingsInspectionService));
    private readonly IDwarfAgent _dwarfAgent = SelectAgent(dwarfAgents);

    public async Task<ChatSessionCreateResult> CreateSessionAsync(string dwarfId, CancellationToken cancellationToken)
    {
        if (!TryParseDwarfId(dwarfId, out var parsedDwarfId))
        {
            throw new ChatValidationException("invalid_dwarf_id", "The provided dwarf ID is invalid.");
        }

        var listResult = await _dwarfQueryService.ListDwarvesAsync(cancellationToken);
        if (!listResult.List.Items.Any(item => item.Id == parsedDwarfId))
        {
            throw new DwarfNotFoundException(parsedDwarfId);
        }

        var snapshotResult = await _dwarfQueryService.GetDwarfSnapshotAsync(parsedDwarfId, cancellationToken);
        if (snapshotResult.Snapshot.RequestedDwarfId != parsedDwarfId || snapshotResult.Snapshot.Identity.Id != parsedDwarfId)
        {
            throw new DwarfFortressDataException(DwarfFortressDataErrorCode.InconsistentData, "The dwarf snapshot identity does not match the requested dwarf ID.");
        }

        var session = _sessionStore.CreateSession(snapshotResult.Snapshot);
        return new ChatSessionCreateResult(session.SessionId, session.DwarfId.ToString());
    }

    public async Task<ChatSendMessageResult> SendMessageAsync(string sessionId, string message, CancellationToken cancellationToken)
    {
        var parsedSessionId = ParseSessionId(sessionId);
        if (!_sessionStore.TryGetSession(parsedSessionId, out var session) || session is null)
        {
            throw new ChatSessionNotFoundException(parsedSessionId);
        }

        var normalizedMessage = NormalizeMessage(message);
        using var activity = FortressSoulsTelemetry.ActivitySource.StartActivity(FortressSoulsTelemetry.ChatTurnActivityName, ActivityKind.Internal);
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
            var scene = await AcquireCurrentSceneAsync(session, cancellationToken);
            var promptResult = _promptAssembler.AssembleAgentTurn(
                new AgentPromptInputs(
                    Snapshot: session.Snapshot,
                    Conversation: [.. session.Messages.Select(MapPromptConversationMessage)],
                    PlayerMessage: normalizedMessage,
                    EnabledTools: NormalPromptTools,
                    StaticInterpretationGuide: PromptContract.DefaultStaticInterpretationGuide,
                    CurrentScene: scene.Observation,
                    CurrentSceneUnavailable: scene.Unavailable),
                _options.PromptAssembly);

            if (!promptResult.Succeeded || string.IsNullOrEmpty(promptResult.ProviderPromptText))
            {
                activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
                throw new ChatValidationException("prompt_assembly_failed", "Failed to assemble a valid chat prompt.");
            }

            var promptId = CreatePromptId(promptResult.ProviderPromptText);
            var stopwatch = Stopwatch.StartNew();
            AgentTurnResult turnResult;
            try
            {
                turnResult = await _dwarfAgent.RunTurnAsync(
                    new AgentTurnRequest(
                        new AgentSessionContext(session.SessionId, session.DwarfId, session.Snapshot, [.. session.Messages]),
                        normalizedMessage,
                        NormalExecutionPolicy,
                        promptResult.ProviderPromptText)
                    {
                        EnabledToolNames = NormalToolNames
                    },
                    cancellationToken);
            }
            catch (AgentTurnException exception)
            {
                activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
                throw MapAgentFailure(exception);
            }

            stopwatch.Stop();
            var assistantMessage = NormalizeAssistantMessage(turnResult.AssistantMessage);
            AppendSuccessfulTurn(session, normalizedMessage, assistantMessage, promptResult.SafePreviewText ?? CurrentSceneFormatter.CreateUnavailablePreview());

            activity?.SetTag(FortressSoulsTelemetry.ProviderTypeTagName, turnResult.ProviderType);
            activity?.SetTag(FortressSoulsTelemetry.LlmModelTagName, turnResult.Model);
            activity?.SetTag(FortressSoulsTelemetry.PromptTemplateVersionTagName, promptResult.Diagnostics.TemplateVersion);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.SuccessOutcome);

            return new ChatSendMessageResult(
                session.SessionId,
                session.DwarfId.ToString(),
                assistantMessage,
                new ChatTurnDiagnostics(
                    turnResult.ProviderType,
                    turnResult.Model,
                    Math.Max(0, (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero)),
                    promptId),
                turnResult.ToolReceipts,
                [new AgentObservationReceipt("current_scene", scene.Unavailable ? AgentToolOutcomes.Unavailable : AgentToolOutcomes.Success)]);
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

    private async Task<SceneAcquisition> AcquireCurrentSceneAsync(ChatSessionState session, CancellationToken cancellationToken)
    {
        using var activity = FortressSoulsTelemetry.ActivitySource.StartActivity(FortressSoulsTelemetry.PerceptionSceneActivityName, ActivityKind.Internal);
        activity?.SetTag(FortressSoulsTelemetry.DwarfIdTagName, session.DwarfId.ToString());
        try
        {
            var observation = await _surroundingsInspectionService.ObserveCurrentSceneAsync(session.DwarfId, cancellationToken);
            observation.Validate();
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneSchemaVersionTagName, observation.SchemaVersion);
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneOutcomeTagName, FortressSoulsTelemetry.SuccessOutcome);
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneSiteWidthTagName, observation.SiteOverview.Width);
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneSiteHeightTagName, observation.SiteOverview.Height);
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneLocalWidthTagName, observation.LocalMap.Width);
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneLocalHeightTagName, observation.LocalMap.Height);
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneWarningCountTagName, observation.Warnings.Count);
            return new SceneAcquisition(observation, false);
        }
        catch (DwarfFortressDataException exception) when (IsExpectedSceneUnavailable(exception.ErrorCode))
        {
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneOutcomeTagName, AgentToolOutcomes.Unavailable);
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneErrorCategoryTagName, exception.ErrorCode.ToString());
            return new SceneAcquisition(null, true);
        }
        catch (CurrentSceneValidationException exception)
        {
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
            activity?.SetTag(FortressSoulsTelemetry.PerceptionSceneErrorCategoryTagName, "invalid_data");
            throw new DwarfFortressDataException(DwarfFortressDataErrorCode.InvalidData, "The current scene data is invalid.", exception);
        }
    }

    private static bool IsExpectedSceneUnavailable(DwarfFortressDataErrorCode errorCode) =>
        errorCode is DwarfFortressDataErrorCode.SourceUnavailable
            or DwarfFortressDataErrorCode.DfHackUnavailable
            or DwarfFortressDataErrorCode.DfHackExecutableUnavailable
            or DwarfFortressDataErrorCode.DfHackInvocationTimedOut
            or DwarfFortressDataErrorCode.MissingSource;

    private static void EnsureSnapshotIdentity(ChatSessionState session)
    {
        if (session.Snapshot.RequestedDwarfId != session.DwarfId || session.Snapshot.Identity.Id != session.DwarfId)
        {
            throw new ChatValidationException("chat_identity_mismatch", "The chat session dwarf identity is invalid.");
        }
    }

    private void AppendSuccessfulTurn(ChatSessionState session, string playerMessage, string assistantMessage, string safePreview)
    {
        session.Messages.Add(new ChatHistoryMessage(ChatRole.Player, playerMessage));
        session.Messages.Add(new ChatHistoryMessage(ChatRole.Assistant, assistantMessage));
        if (session.Messages.Count > _options.MaxHistoryMessages)
        {
            session.Messages.RemoveRange(0, session.Messages.Count - _options.MaxHistoryMessages);
        }
        session.LastPromptPreview = safePreview;
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
        return normalized.Length > _options.MaxAssistantMessageCharacters ? normalized[.._options.MaxAssistantMessageCharacters] : normalized;
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
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length != 13 || !sessionId.StartsWith("chat-", StringComparison.Ordinal))
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
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(promptText));
        return $"prompt-{Convert.ToHexString(hash[..6]).ToLowerInvariant()}";
    }

    private static ChatProviderException MapAgentFailure(AgentTurnException exception) =>
        exception.ErrorCode switch
        {
            AgentTurnErrorCode.TimedOut => new ChatProviderException(ChatProviderErrorCode.Timeout, "The chat provider timed out.", exception),
            AgentTurnErrorCode.Unavailable => new ChatProviderException(ChatProviderErrorCode.Unavailable, "The chat provider is unavailable.", exception),
            _ => new ChatProviderException(ChatProviderErrorCode.InvalidResponse, "The chat provider returned an invalid response.", exception)
        };

    private static IDwarfAgent SelectAgent(IEnumerable<IDwarfAgent> dwarfAgents)
    {
        ArgumentNullException.ThrowIfNull(dwarfAgents);
        return dwarfAgents.Take(2).ToArray() switch
        {
            [var agent] => agent,
            [] => throw new InvalidOperationException("An IDwarfAgent registration is required for chat."),
            _ => throw new InvalidOperationException("Only one dwarf agent may be registered.")
        };
    }

    private sealed record SceneAcquisition(CurrentSceneObservation? Observation, bool Unavailable);
}
