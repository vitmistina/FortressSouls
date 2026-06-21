namespace FortressSouls.Tests;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using FortressSouls.Domain;
using FortressSouls.Observability;
using FortressSouls.Prompting;

public sealed class PromptAssemblerTests
{
    [Fact]
    public void Assemble_ProducesReviewedGoldenPrompt_ForSyntheticData()
    {
        var assembler = new PromptAssembler();
        var result = assembler.Assemble(
            new PromptInputs(
                Snapshot: CreateSyntheticSnapshot(),
                Conversation:
                [
                    new PromptConversationMessage(PromptMessageRole.Assistant, "Stone stands."),
                    new PromptConversationMessage(PromptMessageRole.Player, "How goes the mine?")
                ],
                PlayerMessage: "Any cave-ins?",
                StaticInterpretationGuide: PromptContract.DefaultStaticInterpretationGuide));

        Assert.True(result.Succeeded);
        Assert.Equal(PromptContract.TemplateVersion, result.Diagnostics.TemplateVersion);
        Assert.Equal(2, result.Diagnostics.ConversationMessagesIncluded);
        Assert.Equal(NormalizeToLfWithTrailingNewline(ReviewedGoldenPrompt), result.PromptText);
    }

    [Fact]
    public void Assemble_IsDeterministicAcrossCultureSettings()
    {
        var assembler = new PromptAssembler();
        var inputs = new PromptInputs(
            Snapshot: CreateSyntheticSnapshot(),
            Conversation:
            [
                new PromptConversationMessage(PromptMessageRole.Player, "İstanbul \r\n stone")
            ],
            PlayerMessage: "Merhaba",
            StaticInterpretationGuide: PromptContract.DefaultStaticInterpretationGuide);

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            var turkishResult = assembler.Assemble(inputs);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var englishResult = assembler.Assemble(inputs);

            Assert.Equal(turkishResult.PromptText, englishResult.PromptText);
            Assert.Equal(turkishResult.Diagnostics, englishResult.Diagnostics);
            Assert.NotNull(turkishResult.PromptText);
            Assert.DoesNotContain('\r', turkishResult.PromptText);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Assemble_DropsOldestConversationMessages_WhenPromptExceedsBudget()
    {
        var assembler = new PromptAssembler();
        var conversation =
            new List<PromptConversationMessage>
            {
                new(PromptMessageRole.Player, new string('a', 300)),
                new(PromptMessageRole.Assistant, new string('b', 300)),
                new(PromptMessageRole.Player, "latest")
            };

        var largeBudgetOptions = new PromptAssemblyOptions(
            MaxPromptCharacters: 20_000,
            MaxConversationMessages: 12,
            MaxConversationMessageCharacters: 400,
            MaxPlayerMessageCharacters: 400,
            MaxStaticGuideCharacters: 2_000);

        var fitsAfterDroppingOldest = assembler.Assemble(
            new PromptInputs(
                Snapshot: CreateSyntheticSnapshot(),
                Conversation: conversation.Skip(1).ToList(),
                PlayerMessage: "current"),
            largeBudgetOptions);

        Assert.True(fitsAfterDroppingOldest.Succeeded);

        var options = largeBudgetOptions with
        {
            MaxPromptCharacters = fitsAfterDroppingOldest.Diagnostics.EstimatedCharacterCount
        };

        var result = assembler.Assemble(
            new PromptInputs(
                Snapshot: CreateSyntheticSnapshot(),
                Conversation: conversation,
                PlayerMessage: "current"),
            options);

        Assert.True(result.Succeeded);
        Assert.True(result.Diagnostics.Truncation.ConversationMessagesDroppedForBudget);
        Assert.False(result.Diagnostics.Truncation.ConversationMessagesDroppedForCount);
        Assert.Equal(2, result.Diagnostics.ConversationMessagesIncluded);
        Assert.DoesNotContain("\"text\":\"aaaaaaaaaa", result.PromptText, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"bbbbbbbbbb", result.PromptText, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"latest\"", result.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_KeepsNewestConversationMessages_WhenMaxConversationMessagesExceeded()
    {
        var assembler = new PromptAssembler();
        var options = new PromptAssemblyOptions(
            MaxPromptCharacters: 10_000,
            MaxConversationMessages: 2,
            MaxConversationMessageCharacters: 200,
            MaxPlayerMessageCharacters: 200,
            MaxStaticGuideCharacters: 200);

        var result = assembler.Assemble(
            new PromptInputs(
                Snapshot: CreateSyntheticSnapshot(),
                Conversation:
                [
                    new PromptConversationMessage(PromptMessageRole.Player, "oldest"),
                    new PromptConversationMessage(PromptMessageRole.Assistant, "middle"),
                    new PromptConversationMessage(PromptMessageRole.Player, "newest")
                ],
                PlayerMessage: "current"),
            options);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Diagnostics.ConversationMessagesIncluded);
        Assert.True(result.Diagnostics.Truncation.ConversationMessagesDroppedForCount);
        Assert.False(result.Diagnostics.Truncation.ConversationMessagesDroppedForBudget);
        Assert.DoesNotContain("oldest", result.PromptText, StringComparison.Ordinal);
        Assert.Contains("middle", result.PromptText, StringComparison.Ordinal);
        Assert.Contains("newest", result.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_TruncatesConversationMessageText_ByConfiguredLimit()
    {
        var assembler = new PromptAssembler();
        var options = new PromptAssemblyOptions(
            MaxPromptCharacters: 10_000,
            MaxConversationMessages: 12,
            MaxConversationMessageCharacters: 5,
            MaxPlayerMessageCharacters: 200,
            MaxStaticGuideCharacters: 200);

        var result = assembler.Assemble(
            new PromptInputs(
                Snapshot: CreateSyntheticSnapshot(),
                Conversation:
                [
                    new PromptConversationMessage(PromptMessageRole.Assistant, "abcdef")
                ],
                PlayerMessage: "ok"),
            options);

        Assert.True(result.Succeeded);
        Assert.True(result.Diagnostics.Truncation.ConversationMessageTextTruncated);
        Assert.Contains("\"text\":\"abcde\"", result.PromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"text\":\"abcdef\"", result.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_TruncatesPlayerMessage_ByConfiguredLimit()
    {
        var assembler = new PromptAssembler();
        var options = new PromptAssemblyOptions(
            MaxPromptCharacters: 10_000,
            MaxConversationMessages: 12,
            MaxConversationMessageCharacters: 200,
            MaxPlayerMessageCharacters: 4,
            MaxStaticGuideCharacters: 200);

        var result = assembler.Assemble(
            new PromptInputs(
                Snapshot: CreateSyntheticSnapshot(),
                Conversation: [],
                PlayerMessage: "12345"),
            options);

        Assert.True(result.Succeeded);
        Assert.True(result.Diagnostics.Truncation.PlayerMessageTruncated);
        Assert.Contains("PLAYER_MESSAGE_JSON:\n{\"text\":\"1234\"}", result.PromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("PLAYER_MESSAGE_JSON:\n{\"text\":\"12345\"}", result.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_TruncatesStaticGuide_ByConfiguredLimit()
    {
        var assembler = new PromptAssembler();
        var options = new PromptAssemblyOptions(
            MaxPromptCharacters: 10_000,
            MaxConversationMessages: 12,
            MaxConversationMessageCharacters: 200,
            MaxPlayerMessageCharacters: 200,
            MaxStaticGuideCharacters: 6);

        var result = assembler.Assemble(
            new PromptInputs(
                Snapshot: CreateSyntheticSnapshot(),
                Conversation: [],
                PlayerMessage: "ok",
                StaticInterpretationGuide: "ABCDEFGHIJ"),
            options);

        Assert.True(result.Succeeded);
        Assert.True(result.Diagnostics.Truncation.StaticGuideTruncated);
        Assert.Contains("INTERPRETATION_GUIDE:\nABCDEF\nCONVERSATION_JSON:", result.PromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("INTERPRETATION_GUIDE:\nABCDEFG", result.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_ReturnsStableValidationFailure_ForInvalidConversationRole()
    {
        var assembler = new PromptAssembler();
        var result = assembler.Assemble(
            new PromptInputs(
                Snapshot: CreateSyntheticSnapshot(),
                Conversation:
                [
                    new PromptConversationMessage((PromptMessageRole)999, "hello")
                ],
                PlayerMessage: "ok"));

        Assert.False(result.Succeeded);
        Assert.Null(result.PromptText);
        Assert.Equal(
            new PromptAssemblyDiagnostics(
                TemplateVersion: PromptContract.TemplateVersion,
                EstimatedCharacterCount: 0,
                EstimatedTokenCount: 0,
                ConversationMessagesIncluded: 0,
                Truncation: new PromptTruncationInfo(false, false, false, false, false),
                FailureCategory: PromptAssemblyFailureCategory.ValidationError),
            result.Diagnostics);
    }

    [Fact]
    public void Assemble_ReturnsPromptTooLarge_WhenBudgetCannotFitWithoutConversation()
    {
        var assembler = new PromptAssembler();
        var options = new PromptAssemblyOptions(
            MaxPromptCharacters: 500,
            MaxConversationMessages: 0,
            MaxConversationMessageCharacters: 100,
            MaxPlayerMessageCharacters: 50,
            MaxStaticGuideCharacters: 100);

        var result = assembler.Assemble(
            new PromptInputs(
                Snapshot: CreateSyntheticSnapshot(),
                Conversation: [],
                PlayerMessage: "hello"),
            options);

        Assert.False(result.Succeeded);
        Assert.Null(result.PromptText);
        Assert.Equal(PromptAssemblyFailureCategory.PromptTooLarge, result.FailureCategory);
        Assert.True(result.Diagnostics.EstimatedCharacterCount > options.MaxPromptCharacters);
    }

    [Fact]
    public void Assemble_EmitsContentFreePromptMetricAndSpanTags()
    {
        var metricMeasurements = new List<(int Measurement, IReadOnlyList<KeyValuePair<string, object?>> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == FortressSoulsTelemetry.MeterName
                && instrument.Name == FortressSoulsTelemetry.PromptTokensEstimatedMetricName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            metricMeasurements.Add((measurement, tags.ToArray()));
        });
        listener.Start();

        Activity? capturedStoppedActivity = null;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FortressSoulsTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == FortressSoulsTelemetry.PromptAssembleActivityName)
                {
                    capturedStoppedActivity = activity;
                }
            }
        };

        ActivitySource.AddActivityListener(activityListener);

        var assembler = new PromptAssembler();
        var result = assembler.Assemble(
            new PromptInputs(
                Snapshot: CreateSyntheticSnapshot(),
                Conversation: [],
                PlayerMessage: "short"));

        Assert.True(result.Succeeded);
        Assert.Single(metricMeasurements);
        Assert.True(metricMeasurements[0].Measurement > 0);
        Assert.Contains(metricMeasurements[0].Tags, tag => tag.Key == FortressSoulsTelemetry.PromptTemplateVersionTagName);
        Assert.Contains(metricMeasurements[0].Tags, tag => tag.Key == FortressSoulsTelemetry.PromptTruncatedTagName);
        Assert.Contains(metricMeasurements[0].Tags, tag => tag.Key == FortressSoulsTelemetry.OperationOutcomeTagName);

        Assert.NotNull(capturedStoppedActivity);
        Assert.Contains(capturedStoppedActivity!.Tags, tag => tag.Key == FortressSoulsTelemetry.PromptTemplateVersionTagName);
        Assert.DoesNotContain(capturedStoppedActivity!.Tags, tag => tag.Key.Contains("prompt", StringComparison.OrdinalIgnoreCase)
            && tag.Key != FortressSoulsTelemetry.PromptTemplateVersionTagName);
    }

    private static DwarfSnapshot CreateSyntheticSnapshot() =>
        new(
            SchemaVersion: DwarfSchemaVersions.Snapshot,
            Source: new DwarfSnapshotSourceMetadata(
                WorldLoaded: true,
                SiteLoaded: true,
                MapLoaded: true,
                SoulPresent: true),
            RequestedDwarfId: DwarfId.Parse("7001"),
            Identity: new DwarfIdentity(
                Id: DwarfId.Parse("7001"),
                ReadableName: "Urist Granitefist",
                ProfessionName: "Miner",
                ProfessionToken: "MINER",
                CreatureId: "DWARF",
                CasteId: "MALE"),
            Work: new DwarfWork(CurrentJobType: "DigChannel"),
            Stress: new DwarfStress(
                Raw: 3,
                Longterm: 2,
                Category: 4,
                CategoryScale: "0-most-stressed-6-least-stressed"),
            Skills: new DwarfSkillCollection(
                Count: 1,
                Items:
                [
                    new DwarfSkill(
                        Token: "MINING",
                        Rating: 6,
                        Effective: 6,
                        Nominal: 6,
                        Experience: 2200,
                        TotalExperience: 4200,
                        Rust: 0)
                ]),
            Personality: new DwarfPersonality(
                Present: true,
                Traits: new DwarfTraitCollection(
                    Count: 1,
                    Items:
                    [
                        new DwarfPersonalityTrait(
                            Token: "ASSERTIVENESS",
                            Value: 73,
                            DeviationFromNeutral50: 23,
                            AbsDeviationFromNeutral50: 23)
                    ]),
                Values: new DwarfValueCollection(
                    Count: 1,
                    Items:
                    [
                        new DwarfValue(
                            Token: "HARD_WORK",
                            Type: 1,
                            Strength: 20)
                    ]),
                Needs: new DwarfNeedCollection(
                    Count: 1,
                    Items:
                    [
                        new DwarfNeed(
                            Token: "DrinkAlcohol",
                            Id: 1,
                            DeityId: -1,
                            FocusLevel: 0,
                            NeedLevel: 5,
                            IsUnmet: false,
                            IsDeeplyUnmet: false)
                    ]),
                Mannerisms: new DwarfMannerismCollection(
                    Count: 1,
                    Items:
                    [
                        new DwarfMannerism(
                            Token: "BEARD_STROKE",
                            SituationToken: "WHEN_THINKING")
                    ])),
            PromptCandidates: new DwarfPromptCandidates(
                TopSkills:
                [
                    new DwarfSkill(
                        Token: "MINING",
                        Rating: 6,
                        Effective: 6,
                        Nominal: 6,
                        Experience: 2200,
                        TotalExperience: 4200,
                        Rust: 0)
                ],
                ExtremeTraits:
                [
                    new DwarfPersonalityTrait(
                        Token: "ASSERTIVENESS",
                        Value: 73,
                        DeviationFromNeutral50: 23,
                        AbsDeviationFromNeutral50: 23)
                ],
                StrongValues:
                [
                    new DwarfValue(
                        Token: "HARD_WORK",
                        Type: 1,
                        Strength: 20)
                ],
                StrongNeeds:
                [
                    new DwarfNeed(
                        Token: "DrinkAlcohol",
                        Id: 1,
                        DeityId: -1,
                        FocusLevel: 0,
                        NeedLevel: 5,
                        IsUnmet: false,
                        IsDeeplyUnmet: false)
                ],
                Mannerisms:
                [
                    new DwarfMannerism(
                        Token: "BEARD_STROKE",
                        SituationToken: "WHEN_THINKING")
                ]));

    private static string NormalizeToLfWithTrailingNewline(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal);
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }

    private const string ReviewedGoldenPrompt =
        """
        TEMPLATE_VERSION: fortress-souls-prompt-template.v0.1
        STATIC_GUIDE_VERSION: fortress-souls-interpretation-guide.v0.1

        SYSTEM:
        You portray a specific dwarf from a Dwarf Fortress settlement.

        Rules:
        - Use only the supplied dwarf state and active conversation.
        - Do not claim to know current surroundings unless the state says so.
        - Do not claim that actions happened unless the state says so.
        - Do not invent game events.
        - Do not act as a generic assistant.
        - If uncertain, say so in character.
        - You may have opinions based on supplied personality, work, needs, and values.
        - Keep responses concise unless the player asks for detail.
        DWARF_STATE_JSON:
        {"schemaVersion":"fortress-souls-dwarf-snapshot.v0.1","source":{"worldLoaded":true,"siteLoaded":true,"mapLoaded":true,"soulPresent":true},"requestedDwarfId":"7001","identity":{"id":"7001","readableName":"Urist Granitefist","professionName":"Miner","professionToken":"MINER","creatureId":"DWARF","casteId":"MALE"},"work":{"currentJobType":"DigChannel"},"stress":{"raw":3,"longterm":2,"category":4,"categoryScale":"0-most-stressed-6-least-stressed"},"skills":{"count":1,"items":[{"token":"MINING","rating":6,"effective":6,"nominal":6,"experience":2200,"totalExperience":4200,"rust":0}]},"personality":{"present":true,"traits":{"count":1,"items":[{"token":"ASSERTIVENESS","value":73,"deviationFromNeutral50":23,"absDeviationFromNeutral50":23}]},"values":{"count":1,"items":[{"token":"HARD_WORK","type":1,"strength":20}]},"needs":{"count":1,"items":[{"token":"DrinkAlcohol","id":1,"deityId":-1,"focusLevel":0,"needLevel":5,"isUnmet":false,"isDeeplyUnmet":false}]},"mannerisms":{"count":1,"items":[{"token":"BEARD_STROKE","situationToken":"WHEN_THINKING"}]}},"promptCandidates":{"topSkills":[{"token":"MINING","rating":6,"effective":6,"nominal":6,"experience":2200,"totalExperience":4200,"rust":0}],"extremeTraits":[{"token":"ASSERTIVENESS","value":73,"deviationFromNeutral50":23,"absDeviationFromNeutral50":23}],"strongValues":[{"token":"HARD_WORK","type":1,"strength":20}],"strongNeeds":[{"token":"DrinkAlcohol","id":1,"deityId":-1,"focusLevel":0,"needLevel":5,"isUnmet":false,"isDeeplyUnmet":false}],"mannerisms":[{"token":"BEARD_STROKE","situationToken":"WHEN_THINKING"}]}}
        INTERPRETATION_GUIDE:
        Trait values are approximate and may be incomplete.
        High assertiveness means the dwarf may openly disagree.
        High anxiety means the dwarf may worry about risks.
        High orderliness means the dwarf prefers plans, routines, and tidy work.
        High anger means the dwarf may react sharply to frustration.
        High dutifulness means the dwarf takes obligations seriously.
        CONVERSATION_JSON:
        [{"role":"assistant","text":"Stone stands."},{"role":"player","text":"How goes the mine?"}]
        PLAYER_MESSAGE_JSON:
        {"text":"Any cave-ins?"}
        """;
}
