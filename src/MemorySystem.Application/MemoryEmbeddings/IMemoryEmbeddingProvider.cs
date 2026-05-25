namespace MemorySystem.Application.MemoryEmbeddings;

public interface IMemoryEmbeddingProvider
{
    string ProviderName { get; }

    string Model { get; }

    int Dimension { get; }

    Task<MemoryEmbeddingVector> EmbedAsync(
        MemoryEmbeddingRequest request,
        CancellationToken cancellationToken = default);
}
