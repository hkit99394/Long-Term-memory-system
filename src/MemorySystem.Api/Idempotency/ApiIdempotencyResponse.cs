namespace MemorySystem.Api.Idempotency;

public sealed record ApiIdempotencyResponse(
    int StatusCode,
    object? Body,
    string? ResourceType = null,
    Guid? ResourceId = null,
    bool IdempotencyAlreadyCompleted = false);
