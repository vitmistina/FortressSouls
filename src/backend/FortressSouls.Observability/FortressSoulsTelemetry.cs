namespace FortressSouls.Observability;

using System.Diagnostics;
using System.Diagnostics.Metrics;

public static class FortressSoulsTelemetry
{
    public const string ActivitySourceName = "FortressSouls";

    public const string MeterName = "FortressSouls";

    public const string CorrelationHeaderName = "X-Correlation-ID";

    public const string CorrelationIdTagName = "correlationId";

    public const string CorrelationIdFieldName = "correlationId";

    public const string SessionIdFieldName = "sessionId";

    public const string DwarfIdFieldName = "dwarfId";

    public const string SnapshotSchemaVersionFieldName = "snapshotSchemaVersion";

    public const string ProviderTypeFieldName = "providerType";

    public const string ModelFieldName = "model";

    public const string OperationFieldName = "operation";

    public const string DurationMsFieldName = "durationMs";

    public const string ErrorCodeFieldName = "errorCode";

    public const string AdapterTypeTagName = "fortresssouls.adapter.type";

    public const string ProviderTypeTagName = "fortresssouls.provider.type";

    public const string LlmModelTagName = "fortresssouls.llm.model";

    public const string DwarfIdTagName = "fortresssouls.dwarf.id";

    public const string SnapshotSchemaVersionTagName = "fortresssouls.snapshot.schema_version";

    public const string PromptTemplateVersionTagName = "fortresssouls.prompt.template_version";

    public const string ChatSessionIdTagName = "fortresssouls.chat.session_id";

    public const string ConsoleFallbackObservabilityState = "ConsoleFallback";

    public const string OtlpConfiguredObservabilityState = "OtlpConfigured";

    public const string StartupCounterName = "fortresssouls.api.startup.count";

    public const string ObservabilityStateTagName = "observability.state";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> StartupCounter = Meter.CreateCounter<long>(StartupCounterName);

    public static void RecordStartup(string observabilityState)
    {
        StartupCounter.Add(1, new TagList
        {
            { ObservabilityStateTagName, observabilityState }
        });
    }
}
