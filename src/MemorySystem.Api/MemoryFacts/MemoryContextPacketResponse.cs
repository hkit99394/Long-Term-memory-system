namespace MemorySystem.Api.MemoryFacts;

public sealed record MemoryContextPacketResponse(
    Guid PacketId,
    Guid PrincipalId,
    string Query,
    MemoryContextTargetScopeResponse? TargetScope,
    string? RoleId,
    MemoryContextCurrentTaskResponse CurrentTask,
    IReadOnlyList<MemoryContextPacketItemResponse> UserPreferences,
    IReadOnlyList<MemoryContextPacketItemResponse> ProjectMemory,
    IReadOnlyList<MemoryContextPacketItemResponse> RoleMemory,
    IReadOnlyList<MemoryContextPacketItemResponse> RelevantDecisions,
    IReadOnlyList<MemoryContextExclusionSummaryResponse> Excluded,
    IReadOnlyList<MemoryContextSourceEventResponse> SourceEvents);

public sealed record MemoryContextCurrentTaskResponse(
    string Query,
    MemoryContextTargetScopeResponse? TargetScope,
    string? RoleId);

public sealed record MemoryContextTargetScopeResponse(
    string ScopeType,
    string ScopeId);

public sealed record MemoryContextPacketItemResponse(
    Guid ItemId,
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
    string Summary,
    string PrimaryReason,
    IReadOnlyList<string> MatchedSignals,
    MemoryContextPolicyFitResponse PolicyFit,
    MemoryContextLifecycleFitResponse LifecycleFit,
    MemoryContextSourceEvidenceResponse SourceEvidence,
    IReadOnlyList<string> ReviewSuggestedActions);

public sealed record MemoryContextPolicyFitResponse(
    bool Authorized,
    bool ScopeMatched,
    bool NamespaceGrantMatched,
    bool? RoleMatched);

public sealed record MemoryContextLifecycleFitResponse(
    string Status,
    bool EvidenceCurrent,
    string RedactionStatus);

public sealed record MemoryContextSourceEvidenceResponse(
    IReadOnlyList<Guid> SourceEventIds,
    IReadOnlyList<string> SourceLinks,
    bool SourceLinked);

public sealed record MemoryContextSourceEventResponse(
    Guid Id,
    string? Link);

public sealed record MemoryContextExclusionSummaryResponse(
    string Reason,
    int? Count,
    string CountDisclosure,
    string SafeSummary,
    IReadOnlyList<string> ReviewActions);
