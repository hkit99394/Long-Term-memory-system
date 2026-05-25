namespace MemorySystem.Application.MemoryChunks;

public interface IMemoryChunkHybridSearch
{
    Task<IReadOnlyList<MemoryChunkHybridSearchResult>> SearchAsync(
        MemoryChunkHybridSearchQuery query,
        CancellationToken cancellationToken = default);
}
