namespace MemorySystem.Application.MemoryChunks;

public interface IMemoryChunkSemanticSearch
{
    Task<IReadOnlyList<MemoryChunkSearchResult>> SearchAsync(
        MemoryChunkSemanticSearchQuery query,
        CancellationToken cancellationToken = default);
}
