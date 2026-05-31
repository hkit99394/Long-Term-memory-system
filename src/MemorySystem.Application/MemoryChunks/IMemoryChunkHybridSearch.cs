namespace MemorySystem.Application.MemoryChunks;

public interface IMemoryChunkHybridSearch
{
    Task<MemoryChunkHybridSearchResultSet> SearchAsync(
        MemoryChunkHybridSearchQuery query,
        CancellationToken cancellationToken = default);
}
