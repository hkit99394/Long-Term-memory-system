namespace MemorySystem.Application.MemoryEmbeddings;

public interface IMemoryChunkEmbeddingStore
{
    Task StoreAsync(
        MemoryChunkEmbeddingWriteCommand command,
        CancellationToken cancellationToken = default);
}
