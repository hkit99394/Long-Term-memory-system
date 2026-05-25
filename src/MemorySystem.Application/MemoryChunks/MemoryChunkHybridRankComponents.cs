namespace MemorySystem.Application.MemoryChunks;

public sealed record MemoryChunkHybridRankComponents(
    double Relevance,
    double Confidence,
    double Recency,
    double Authority,
    double ScopeMatch);
