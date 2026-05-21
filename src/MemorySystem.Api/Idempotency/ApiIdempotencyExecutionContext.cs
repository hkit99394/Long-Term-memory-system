namespace MemorySystem.Api.Idempotency;

public sealed record ApiIdempotencyExecutionContext(Guid RecordId, string RequestHash);
