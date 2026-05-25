namespace MemorySystem.Application.MemoryContext;

public sealed record MemoryContextSourceEvent(
    Guid Id,
    string Link);
