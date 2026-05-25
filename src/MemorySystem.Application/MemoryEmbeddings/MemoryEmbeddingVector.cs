namespace MemorySystem.Application.MemoryEmbeddings;

public sealed record MemoryEmbeddingVector(
    string Model,
    int Dimension,
    IReadOnlyList<float> Values);
