namespace MemorySystem.Infrastructure.Outbox;

public sealed record OutboxJob(
    Guid Id,
    string JobType,
    string AggregateType,
    Guid AggregateId,
    string IdempotencyKey,
    string PayloadJson,
    string Status,
    int Attempts,
    DateTimeOffset AvailableAt,
    DateTimeOffset? LockedUntil,
    string? LockedBy,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
