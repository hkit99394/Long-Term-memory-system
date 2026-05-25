namespace MemorySystem.Api.MemoryFacts;

public sealed record MemorySearchResponse(
    IReadOnlyList<MemorySearchResultResponse> Results);

public sealed record MemorySearchResultResponse(
    Guid ChunkId,
    string SourceType,
    Guid SourceId,
    string Namespace,
    string ScopeType,
    string ScopeId,
    string? Title,
    string Content,
    double Rank,
    string TrustLevel,
    Guid SourceEventId);
