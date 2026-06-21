namespace FortressSouls.Application;

public sealed record DwarfAdapterStatus(
    string AdapterType,
    bool IsConfigured,
    bool IsReady,
    string LastOutcome,
    string? LastErrorCategory,
    int? LastDurationMs,
    DateTimeOffset? LastUpdatedAtUtc);

public interface IDwarfAdapterStatusReader
{
    DwarfAdapterStatus GetCurrentStatus();
}
