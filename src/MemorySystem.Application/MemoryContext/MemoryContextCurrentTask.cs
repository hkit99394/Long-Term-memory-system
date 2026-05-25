namespace MemorySystem.Application.MemoryContext;

public sealed record MemoryContextCurrentTask(
    string Query,
    MemoryContextTargetScope? TargetScope,
    string? RoleId);
