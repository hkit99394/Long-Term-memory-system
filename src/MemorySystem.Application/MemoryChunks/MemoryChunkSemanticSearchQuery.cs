namespace MemorySystem.Application.MemoryChunks;

public sealed record MemoryChunkSemanticSearchQuery(
    Guid PrincipalId,
    string Query,
    int Limit = 20);
