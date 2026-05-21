namespace MemorySystem.Infrastructure.Events;

public sealed record AppendEventResult(Guid Id, DateTimeOffset CreatedAt);
