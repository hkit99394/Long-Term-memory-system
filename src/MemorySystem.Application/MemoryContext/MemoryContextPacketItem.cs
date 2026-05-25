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
    string Summary);
