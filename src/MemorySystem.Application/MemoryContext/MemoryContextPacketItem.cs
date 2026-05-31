using MemorySystem.Application.MemoryChunks;

namespace MemorySystem.Application.MemoryContext;

public sealed record MemoryContextPacketItem(
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
    MemoryContextExplanation Explanation);

public sealed record MemoryContextExplanation(
    double Rank,
    MemoryChunkHybridRankComponents Components,
    string Summary,
    string PrimaryReason,
    IReadOnlyList<string> MatchedSignals,
    MemoryContextPolicyFit PolicyFit,
    MemoryContextLifecycleFit LifecycleFit,
    MemoryContextSourceEvidence SourceEvidence,
    IReadOnlyList<string> ReviewSuggestedActions);

public sealed record MemoryContextPolicyFit(
    bool Authorized,
    bool ScopeMatched,
    bool NamespaceGrantMatched,
    bool? RoleMatched);

public sealed record MemoryContextLifecycleFit(
    string Status,
    bool EvidenceCurrent,
    string RedactionStatus);

public sealed record MemoryContextSourceEvidence(
    IReadOnlyList<Guid> SourceEventIds,
    IReadOnlyList<string> SourceLinks,
    bool SourceLinked);
