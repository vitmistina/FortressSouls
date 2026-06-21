namespace FortressSouls.Prompting;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FortressSouls.Observability;

public sealed class PromptAssembler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public PromptAssemblyResult Assemble(PromptInputs inputs, PromptAssemblyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        options ??= PromptAssemblyOptions.Default;

        using var activity = FortressSoulsTelemetry.ActivitySource.StartActivity(
            FortressSoulsTelemetry.PromptAssembleActivityName,
            ActivityKind.Internal);

        activity?.SetTag(FortressSoulsTelemetry.PromptTemplateVersionTagName, PromptContract.TemplateVersion);

        PromptAssemblyResult result;
        try
        {
            result = AssembleCore(inputs, options);
        }
        catch
        {
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
            throw;
        }

        var outcome = result.Succeeded ? FortressSoulsTelemetry.SuccessOutcome : FortressSoulsTelemetry.ErrorOutcome;
        activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, outcome);

        FortressSoulsTelemetry.RecordPromptTokensEstimated(
            result.Diagnostics.EstimatedTokenCount,
            PromptContract.TemplateVersion,
            result.Diagnostics.Truncation.Any,
            outcome);

        return result;
    }

    private static PromptAssemblyResult AssembleCore(PromptInputs inputs, PromptAssemblyOptions options)
    {
        if (inputs.Snapshot is null)
        {
            return CreateValidationFailure();
        }

        if (options.MaxPromptCharacters <= 0
            || options.MaxConversationMessages < 0
            || options.MaxConversationMessageCharacters <= 0
            || options.MaxPlayerMessageCharacters <= 0
            || options.MaxStaticGuideCharacters <= 0)
        {
            return CreateValidationFailure();
        }

        if (inputs.PlayerMessage is null)
        {
            return CreateValidationFailure();
        }

        var staticGuide = inputs.StaticInterpretationGuide ?? PromptContract.DefaultStaticInterpretationGuide;
        var normalizedGuide = NormalizeNewlines(staticGuide);
        var normalizedPlayerMessage = NormalizeNewlines(inputs.PlayerMessage);

        if (string.IsNullOrWhiteSpace(normalizedPlayerMessage))
        {
            return CreateValidationFailure();
        }

        var guideTruncated = false;
        var playerMessageTruncated = false;
        var conversationMessageTextTruncated = false;
        var conversationMessagesDroppedForCount = false;
        var conversationMessagesDroppedForBudget = false;

        normalizedGuide = Truncate(normalizedGuide, options.MaxStaticGuideCharacters, ref guideTruncated);
        normalizedPlayerMessage = Truncate(normalizedPlayerMessage, options.MaxPlayerMessageCharacters, ref playerMessageTruncated);

        var normalizedConversation = new List<PromptConversationPayload>();
        var sourceConversation = inputs.Conversation ?? [];
        foreach (var message in sourceConversation)
        {
            if (message is null || message.Text is null)
            {
                return CreateValidationFailure();
            }

            if (!TryNormalizeConversationRole(message.Role, out var normalizedRole))
            {
                return CreateValidationFailure();
            }

            var messageText = NormalizeNewlines(message.Text);
            messageText = Truncate(messageText, options.MaxConversationMessageCharacters, ref conversationMessageTextTruncated);
            normalizedConversation.Add(new PromptConversationPayload(normalizedRole, messageText));
        }

        if (normalizedConversation.Count > options.MaxConversationMessages)
        {
            var skipCount = normalizedConversation.Count - options.MaxConversationMessages;
            normalizedConversation = [.. normalizedConversation.Skip(skipCount)];
            conversationMessagesDroppedForCount = true;
        }

        var snapshotJson = JsonSerializer.Serialize(inputs.Snapshot, SerializerOptions);
        var conversationJson = JsonSerializer.Serialize(normalizedConversation, SerializerOptions);
        var playerJson = JsonSerializer.Serialize(new PromptPlayerPayload(normalizedPlayerMessage), SerializerOptions);

        var promptText = BuildPrompt(snapshotJson, normalizedGuide, conversationJson, playerJson);
        while (promptText.Length > options.MaxPromptCharacters && normalizedConversation.Count > 0)
        {
            normalizedConversation.RemoveAt(0);
            conversationMessagesDroppedForBudget = true;
            conversationJson = JsonSerializer.Serialize(normalizedConversation, SerializerOptions);
            promptText = BuildPrompt(snapshotJson, normalizedGuide, conversationJson, playerJson);
        }

        var truncation = new PromptTruncationInfo(
            ConversationMessagesDroppedForCount: conversationMessagesDroppedForCount,
            ConversationMessagesDroppedForBudget: conversationMessagesDroppedForBudget,
            ConversationMessageTextTruncated: conversationMessageTextTruncated,
            PlayerMessageTruncated: playerMessageTruncated,
            StaticGuideTruncated: guideTruncated);

        if (promptText.Length > options.MaxPromptCharacters)
        {
            return CreateFailure(
                PromptAssemblyFailureCategory.PromptTooLarge,
                truncation,
                promptText.Length,
                normalizedConversation.Count);
        }

        var diagnostics = CreateDiagnostics(PromptAssemblyFailureCategory.None, truncation, promptText.Length, normalizedConversation.Count);
        return new PromptAssemblyResult(promptText, diagnostics);
    }

    private static string BuildPrompt(string snapshotJson, string normalizedGuide, string conversationJson, string playerJson)
    {
        var normalizedSystemInstruction = NormalizeNewlines(PromptContract.SystemInstruction);
        var builder = new StringBuilder(capacity: snapshotJson.Length + normalizedGuide.Length + conversationJson.Length + playerJson.Length + 512);
        builder.Append("TEMPLATE_VERSION: ").Append(PromptContract.TemplateVersion).Append('\n');
        builder.Append("STATIC_GUIDE_VERSION: ").Append(PromptContract.StaticGuideVersion).Append('\n');
        builder.Append('\n');
        builder.Append("SYSTEM:\n").Append(normalizedSystemInstruction).Append('\n');
        builder.Append("DWARF_STATE_JSON:\n").Append(snapshotJson).Append('\n');
        builder.Append("INTERPRETATION_GUIDE:\n").Append(normalizedGuide).Append('\n');
        builder.Append("CONVERSATION_JSON:\n").Append(conversationJson).Append('\n');
        builder.Append("PLAYER_MESSAGE_JSON:\n").Append(playerJson).Append('\n');
        return builder.ToString();
    }

    private static PromptAssemblyResult CreateFailure(
        PromptAssemblyFailureCategory category,
        PromptTruncationInfo truncation,
        int estimatedCharacterCount,
        int conversationMessagesIncluded)
    {
        var diagnostics = CreateDiagnostics(category, truncation, estimatedCharacterCount, conversationMessagesIncluded);
        return new PromptAssemblyResult(null, diagnostics);
    }

    private static PromptAssemblyResult CreateValidationFailure() =>
        CreateFailure(
            PromptAssemblyFailureCategory.ValidationError,
            new PromptTruncationInfo(false, false, false, false, false),
            estimatedCharacterCount: 0,
            conversationMessagesIncluded: 0);

    private static PromptAssemblyDiagnostics CreateDiagnostics(
        PromptAssemblyFailureCategory failureCategory,
        PromptTruncationInfo truncation,
        int estimatedCharacterCount,
        int conversationMessagesIncluded) =>
        new(
            TemplateVersion: PromptContract.TemplateVersion,
            EstimatedCharacterCount: estimatedCharacterCount,
            EstimatedTokenCount: EstimateTokens(estimatedCharacterCount),
            ConversationMessagesIncluded: conversationMessagesIncluded,
            Truncation: truncation,
            FailureCategory: failureCategory);

    private static int EstimateTokens(int characterCount) =>
        characterCount <= 0 ? 0 : (characterCount + 3) / 4;

    private static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

    private static string Truncate(string input, int maxLength, ref bool truncated)
    {
        if (input.Length <= maxLength)
        {
            return input;
        }

        truncated = true;
        return input[..maxLength];
    }

    private static bool TryNormalizeConversationRole(PromptMessageRole role, out string normalizedRole)
    {
        switch (role)
        {
            case PromptMessageRole.Player:
                normalizedRole = "player";
                return true;
            case PromptMessageRole.Assistant:
                normalizedRole = "assistant";
                return true;
            default:
                normalizedRole = string.Empty;
                return false;
        }
    }

    private sealed record PromptConversationPayload(
        string Role,
        string Text);

    private sealed record PromptPlayerPayload(
        string Text);
}
