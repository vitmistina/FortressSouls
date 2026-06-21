namespace FortressSouls.Llm;

using FortressSouls.Application;

public interface IChatProviderStatusRecorder
{
    void RecordSuccess(TimeSpan duration);
    void RecordTimeout(TimeSpan? duration);
    void RecordCancellation();
    void RecordFailure(string errorCategory, TimeSpan? duration = null);
}

public sealed class ChatProviderStatusTracker : IChatProviderStatusRecorder, IChatProviderStatusReader
{
    private readonly object _gate = new();
    private StatusState _state;

    public ChatProviderStatusTracker(LlmProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var model = options.ProviderType == LlmProviderType.Fake ? "fake-dwarf" : options.Model;
        var missingKey = options.ProviderType == LlmProviderType.OpenAiCompatible && string.IsNullOrWhiteSpace(options.ApiKey);
        _state = new StatusState(
            ProviderType: options.ProviderType.ToString(),
            Model: model,
            IsConfigured: !missingKey,
            IsReady: !missingKey,
            LastOutcome: missingKey ? "error" : "not_started",
            LastErrorCategory: missingKey ? "missing_api_key" : null,
            LastDurationMs: null,
            LastUpdatedAtUtc: null);
    }

    public ChatProviderStatus GetCurrentStatus()
    {
        lock (_gate)
        {
            return _state.ToContract();
        }
    }

    public void RecordSuccess(TimeSpan duration)
    {
        lock (_gate)
        {
            _state = _state with
            {
                IsReady = true,
                LastOutcome = "success",
                LastErrorCategory = null,
                LastDurationMs = ToDuration(duration),
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    public void RecordTimeout(TimeSpan? duration)
    {
        lock (_gate)
        {
            _state = _state with
            {
                IsReady = true,
                LastOutcome = "timeout",
                LastErrorCategory = "timeout",
                LastDurationMs = ToDuration(duration),
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    public void RecordCancellation()
    {
        lock (_gate)
        {
            _state = _state with
            {
                IsReady = true,
                LastOutcome = "cancelled",
                LastErrorCategory = "cancelled",
                LastDurationMs = null,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    public void RecordFailure(string errorCategory, TimeSpan? duration = null)
    {
        var safeErrorCategory = string.IsNullOrWhiteSpace(errorCategory) ? "provider_error" : errorCategory;
        lock (_gate)
        {
            _state = _state with
            {
                IsReady = safeErrorCategory != "invalid_configuration" && safeErrorCategory != "missing_api_key",
                LastOutcome = "error",
                LastErrorCategory = safeErrorCategory,
                LastDurationMs = ToDuration(duration),
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    private static int? ToDuration(TimeSpan? duration) =>
        duration is null
            ? null
            : Math.Max(0, (int)Math.Round(duration.Value.TotalMilliseconds, MidpointRounding.AwayFromZero));

    private sealed record StatusState(
        string ProviderType,
        string Model,
        bool IsConfigured,
        bool IsReady,
        string LastOutcome,
        string? LastErrorCategory,
        int? LastDurationMs,
        DateTimeOffset? LastUpdatedAtUtc)
    {
        public ChatProviderStatus ToContract() =>
            new(
                ProviderType,
                Model,
                IsConfigured,
                IsReady,
                LastOutcome,
                LastErrorCategory,
                LastDurationMs,
                LastUpdatedAtUtc);
    }
}
