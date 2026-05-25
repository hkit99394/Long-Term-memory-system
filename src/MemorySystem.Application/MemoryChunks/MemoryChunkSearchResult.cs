namespace MemorySystem.Application.MemoryChunks;

public sealed record MemoryChunkSearchResult(
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
