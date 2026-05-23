namespace MemorySystem.Application.Events;

public sealed record AppendEventResult(Guid Id, DateTimeOffset CreatedAt);
