namespace MemorySystem.Infrastructure.Workers;

public sealed record WorkerHeartbeatUpdate(
    string WorkerType,
    string WorkerId,
    string Status,
    string? LastError = null);
