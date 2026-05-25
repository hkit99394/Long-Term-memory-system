namespace MemorySystem.Api.MemoryFacts;

public sealed record MemoryContextPacketResponse(
    Guid PrincipalId,
    string Query,
    MemoryContextTargetScopeResponse? TargetScope,
    string? RoleId,
    MemoryContextCurrentTaskResponse CurrentTask,
    IReadOnlyList<MemoryContextPacketItemResponse> UserPreferences,
    IReadOnlyList<MemoryContextPacketItemResponse> ProjectMemory,
    IReadOnlyList<MemoryContextPacketItemResponse> RoleMemory,
    IReadOnlyList<MemoryContextPacketItemResponse> RelevantDecisions,
    IReadOnlyList<MemoryContextSourceEventResponse> SourceEvents);

public sealed record MemoryContextCurrentTaskResponse(
    string Query,
    MemoryContextTargetScopeResponse? TargetScope,
    string? RoleId);

public sealed record MemoryContextTargetScopeResponse(
    string ScopeType,
    string ScopeId);

public sealed record MemoryContextPacketItemResponse(
    string Kind,
    Guid ChunkId,
    string SourceType,
    Guid SourceId,
    Guid? BaseMemoryFactId,
    string Namespace,
    string ScopeType,
    string ScopeId,
    string? Title,
    string Content,
    double Rank,
    string TrustLevel,
    Guid SourceEventId,
    string? SourceLink,
    MemoryContextExplanationResponse Explanation);

public sealed record MemoryContextExplanationResponse(
    double Rank,
    MemoryHybridRankComponentsResponse Components,
    string Summary);

public sealed record MemoryContextSourceEventResponse(
    Guid Id,
    string? Link);
