namespace MemorySystem.Infrastructure.Workers;

public sealed record WorkerHeartbeatSnapshot(
    string WorkerType,
    string WorkerId,
    string Status,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? LastSuccessAt,
    string? LastError);
