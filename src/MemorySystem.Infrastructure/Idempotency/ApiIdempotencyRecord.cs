namespace MemorySystem.Infrastructure.Idempotency;

public sealed record ApiIdempotencyRecord(
    Guid Id,
    Guid PrincipalId,
    string Endpoint,
    string IdempotencyKey,
    string RequestHash,
    int? ResponseStatus,
    string? ResponseBody,
    string? ResponseContentType,
    string? ResourceType,
    Guid? ResourceId,
    string Status,
    DateTimeOffset ExpiresAt);
