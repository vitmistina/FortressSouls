namespace FortressSouls.Llm;

internal enum LlmProviderErrorCode
{
    InvalidRequest,
    Unavailable,
    InvalidResponse,
    InvalidConfiguration,
    Timeout,
    ResponseTooLarge
}

internal sealed class LlmProviderException : Exception
{
    public LlmProviderException(LlmProviderErrorCode errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public LlmProviderErrorCode ErrorCode { get; }
}
