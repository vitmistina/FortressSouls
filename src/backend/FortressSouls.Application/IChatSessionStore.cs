namespace FortressSouls.Application;

using FortressSouls.Domain;

public interface IChatSessionStore
{
    ChatSessionState CreateSession(DwarfSnapshot snapshot);

    bool TryGetSession(string sessionId, out ChatSessionState? session);
}

public sealed class ChatSessionState(
    string sessionId,
    DwarfSnapshot snapshot,
    long createdOrder)
{
    public string SessionId { get; } = sessionId;

    public DwarfSnapshot Snapshot { get; } = snapshot;

    public DwarfId DwarfId { get; } = snapshot.RequestedDwarfId;

    public long CreatedOrder { get; } = createdOrder;

    public List<ChatHistoryMessage> Messages { get; } = [];

    public string? LastPromptPreview { get; set; }

    public SemaphoreSlim TurnLock { get; } = new(1, 1);
}
