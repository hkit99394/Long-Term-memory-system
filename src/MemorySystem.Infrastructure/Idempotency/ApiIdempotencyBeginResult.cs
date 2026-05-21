namespace MemorySystem.Infrastructure.Idempotency;

public sealed record ApiIdempotencyBeginResult(
    ApiIdempotencyBeginStatus Status,
    ApiIdempotencyRecord Record);
