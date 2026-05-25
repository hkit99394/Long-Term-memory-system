namespace MemorySystem.Application.MemoryChunks;

public sealed record MemoryChunkHybridSearchQuery(
    Guid PrincipalId,
    string Query,
    int Limit = 20,
    string? TargetScopeType = null,
    string? TargetScopeId = null);
