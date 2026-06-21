namespace FortressSouls.Application;

using FortressSouls.Domain;

public sealed class InMemoryChatSessionStore(ChatSessionOptions options) : IChatSessionStore
{
    private readonly ChatSessionOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly object _gate = new();
    private readonly Dictionary<string, ChatSessionState> _sessions = new(StringComparer.Ordinal);
    private long _createdCounter;

    public ChatSessionState CreateSession(DwarfSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateOptions(_options);

        lock (_gate)
        {
            if (_sessions.Count >= _options.MaxSessions)
            {
                EvictOldestSession();
            }

            var createdOrder = Interlocked.Increment(ref _createdCounter);
            var sessionId = $"chat-{createdOrder:D8}";
            var state = new ChatSessionState(sessionId, snapshot, createdOrder);
            _sessions[sessionId] = state;
            return state;
        }
    }

    public bool TryGetSession(string sessionId, out ChatSessionState? session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        lock (_gate)
        {
            return _sessions.TryGetValue(sessionId, out session);
        }
    }

    private void EvictOldestSession()
    {
        var oldest = _sessions.Values
            .OrderBy(static session => session.CreatedOrder)
            .FirstOrDefault();

        if (oldest is null)
        {
            return;
        }

        _sessions.Remove(oldest.SessionId);
    }

    private static void ValidateOptions(ChatSessionOptions options)
    {
        if (options.MaxSessions <= 0
            || options.MaxHistoryMessages <= 0
            || options.MaxPlayerMessageCharacters <= 0
            || options.MaxAssistantMessageCharacters <= 0)
        {
            throw new ChatValidationException("chat_configuration_invalid", "The chat session configuration is invalid.");
        }
    }
}
