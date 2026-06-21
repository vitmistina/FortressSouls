namespace FortressSouls.DwarfFortress;

using FortressSouls.Application;

public interface IDfHackAdapterStatusRecorder
{
    void RecordNotConfigured();
    void RecordSuccess(TimeSpan duration);
    void RecordFailure(string outcome, string errorCategory, TimeSpan? duration = null);
}

public sealed class DfHackAdapterStatusTracker : IDfHackAdapterStatusRecorder, IDwarfAdapterStatusReader
{
    private readonly object _gate = new();
    private StatusState _state;

    public DfHackAdapterStatusTracker(bool enabled)
    {
        _state = new StatusState(
            AdapterType: enabled ? "DfHackProcess" : "Fake",
            IsConfigured: enabled,
            IsReady: enabled,
            LastOutcome: enabled ? "not_started" : "disabled",
            LastErrorCategory: enabled ? null : "adapter_disabled",
            LastDurationMs: null,
            LastUpdatedAtUtc: null);
    }

    public DwarfAdapterStatus GetCurrentStatus()
    {
        lock (_gate)
        {
            return _state.ToContract();
        }
    }

    public void RecordNotConfigured()
    {
        lock (_gate)
        {
            _state = _state with
            {
                IsConfigured = false,
                IsReady = false,
                LastOutcome = "error",
                LastErrorCategory = "invalid_configuration",
                LastDurationMs = null,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    public void RecordSuccess(TimeSpan duration)
    {
        lock (_gate)
        {
            _state = _state with
            {
                IsConfigured = true,
                IsReady = true,
                LastOutcome = "success",
                LastErrorCategory = null,
                LastDurationMs = ToDuration(duration),
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    public void RecordFailure(string outcome, string errorCategory, TimeSpan? duration = null)
    {
        var safeOutcome = string.IsNullOrWhiteSpace(outcome) ? "error" : outcome;
        var safeCategory = string.IsNullOrWhiteSpace(errorCategory) ? "dfhack_error" : errorCategory;
        lock (_gate)
        {
            _state = _state with
            {
                IsConfigured = true,
                IsReady = safeCategory is not "invalid_configuration" and not "executable_unavailable",
                LastOutcome = safeOutcome,
                LastErrorCategory = safeCategory,
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
        string AdapterType,
        bool IsConfigured,
        bool IsReady,
        string LastOutcome,
        string? LastErrorCategory,
        int? LastDurationMs,
        DateTimeOffset? LastUpdatedAtUtc)
    {
        public DwarfAdapterStatus ToContract() =>
            new(
                AdapterType,
                IsConfigured,
                IsReady,
                LastOutcome,
                LastErrorCategory,
                LastDurationMs,
                LastUpdatedAtUtc);
    }
}
