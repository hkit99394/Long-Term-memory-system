namespace MemorySystem.Infrastructure.Idempotency;

public interface IApiIdempotencyStore
{
    Task<ApiIdempotencyBeginResult> BeginAsync(
        Guid principalId,
        string endpoint,
        string idempotencyKey,
        string requestHash,
        IReadOnlyCollection<string> acceptedRequestHashes,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid recordId,
        string requestHash,
        int responseStatus,
        string? responseBody,
        string? responseContentType,
        string? resourceType,
        Guid? resourceId,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid recordId,
        string requestHash,
        CancellationToken cancellationToken = default);
}
