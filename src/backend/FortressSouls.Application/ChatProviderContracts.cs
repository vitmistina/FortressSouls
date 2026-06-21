namespace FortressSouls.Application;

public sealed record ChatProviderRequest(
    string PromptText,
    int MaxResponseCharacters);

public sealed record ChatProviderResponse(
    string MessageText,
    string ProviderType,
    string Model,
    TimeSpan Duration);

public interface IChatProvider
{
    Task<ChatProviderResponse> SendAsync(ChatProviderRequest request, CancellationToken cancellationToken);
}

public enum ChatProviderErrorCode
{
    InvalidRequest,
    Unavailable,
    InvalidResponse,
    InvalidConfiguration,
    Timeout,
    ResponseTooLarge
}

public sealed class ChatProviderException : Exception
{
    public ChatProviderException(ChatProviderErrorCode errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public ChatProviderErrorCode ErrorCode { get; }
}
