namespace MemorySystem.Api.Idempotency;

public sealed record ApiIdempotencyResponse(
    int StatusCode,
    object? Body,
    string? ResourceType = null,
    Guid? ResourceId = null,
    // Set only when the operation store completed idempotency in the same transaction as its durable write.
    bool IdempotencyAlreadyCompleted = false,
    string? ContentType = null);
