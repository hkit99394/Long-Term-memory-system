namespace MemorySystem.Application.MemoryEmbeddings;

public sealed record MemoryChunkEmbeddingWriteCommand(
    Guid ChunkId,
    string Model,
    int Dimension,
    IReadOnlyList<float> Values,
    string? ExpectedContentHash = null,
    Guid? ExpectedSourceEventId = null,
    string? ExpectedSourceType = null,
    Guid? ExpectedSourceId = null);
