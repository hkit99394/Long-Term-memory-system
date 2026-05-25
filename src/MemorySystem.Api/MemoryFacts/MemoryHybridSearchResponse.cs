namespace MemorySystem.Api.MemoryFacts;

public sealed record MemoryHybridSearchResponse(
    IReadOnlyList<MemoryHybridSearchResultResponse> Results);

public sealed record MemoryHybridSearchResultResponse(
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
    MemoryHybridRankComponentsResponse Components);

public sealed record MemoryHybridRankComponentsResponse(
    double Relevance,
    double Confidence,
    double Recency,
    double Authority,
    double ScopeMatch);
