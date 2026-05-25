namespace MemorySystem.Application.MemoryChunks;

public interface IMemoryChunkFullTextSearch
{
    Task<IReadOnlyList<MemoryChunkSearchResult>> SearchAsync(
        MemoryChunkFullTextSearchQuery query,
        CancellationToken cancellationToken = default);
}
