namespace MemorySystem.Application.MemoryChunks;

public sealed record MemoryChunkFullTextSearchQuery(
    Guid PrincipalId,
    string Query,
    int Limit = 20);
