namespace MemorySystem.Application.MemoryContext;

public sealed record MemoryContextPacket(
    Guid PrincipalId,
    string Query,
    MemoryContextTargetScope? TargetScope,
    string? RoleId,
    MemoryContextCurrentTask CurrentTask,
    IReadOnlyList<MemoryContextPacketItem> UserPreferences,
    IReadOnlyList<MemoryContextPacketItem> ProjectMemory,
    IReadOnlyList<MemoryContextPacketItem> RoleMemory,
    IReadOnlyList<MemoryContextPacketItem> RelevantDecisions,
    IReadOnlyList<MemoryContextSourceEvent> SourceEvents);
