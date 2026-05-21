namespace MemorySystem.Infrastructure.Idempotency;

public enum ApiIdempotencyBeginStatus
{
    Started,
    Replay,
    Conflict,
    Processing,
    PreviousFailed
}
