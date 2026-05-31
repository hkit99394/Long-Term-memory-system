namespace MemorySystem.Application.MemoryChunks;

public sealed record MemoryChunkHybridSearchResultSet(
    IReadOnlyList<MemoryChunkHybridSearchResult> Results,
    IReadOnlyList<MemoryChunkHybridExclusionSummary> Exclusions);

public sealed record MemoryChunkHybridSearchResult(
    Guid ChunkId,
    string SourceType,
    Guid SourceId,
    string MemoryKind,
    Guid? BaseMemoryFactId,
    string Namespace,
    string ScopeType,
    string ScopeId,
    string? Title,
    string Content,
    double Rank,
    string TrustLevel,
    Guid SourceEventId,
    MemoryChunkHybridRankComponents Components);

public sealed record MemoryChunkHybridExclusionSummary(
    string Reason,
    int? Count,
    string CountDisclosure = "disclosed");
