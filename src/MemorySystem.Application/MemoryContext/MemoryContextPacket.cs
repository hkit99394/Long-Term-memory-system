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
    IReadOnlyList<MemoryContextExclusionSummary> Excluded,
    IReadOnlyList<MemoryContextSourceEvent> SourceEvents);

public sealed record MemoryContextExclusionSummary(
    string Reason,
    int? Count,
    string CountDisclosure,
    string SafeSummary,
    IReadOnlyList<string> ReviewActions);
