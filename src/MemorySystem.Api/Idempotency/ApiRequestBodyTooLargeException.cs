namespace MemorySystem.Api.Idempotency;

internal sealed class ApiRequestBodyTooLargeException : InvalidOperationException
{
    public ApiRequestBodyTooLargeException(long maxBodyBytes)
        : base($"Request body exceeds the configured limit of {maxBodyBytes} bytes.")
    {
        MaxBodyBytes = maxBodyBytes;
    }

    public ApiRequestBodyTooLargeException(long maxBodyBytes, Exception innerException)
        : base($"Request body exceeds the configured limit of {maxBodyBytes} bytes.", innerException)
    {
        MaxBodyBytes = maxBodyBytes;
    }

    public long MaxBodyBytes { get; }
}
