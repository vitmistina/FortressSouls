namespace FortressSouls.Application;

public sealed record ChatProviderStatus(
    string ProviderType,
    string Model,
    bool IsConfigured,
    bool IsReady,
    string LastOutcome,
    string? LastErrorCategory,
    int? LastDurationMs,
    DateTimeOffset? LastUpdatedAtUtc);

public interface IChatProviderStatusReader
{
    ChatProviderStatus GetCurrentStatus();
}
