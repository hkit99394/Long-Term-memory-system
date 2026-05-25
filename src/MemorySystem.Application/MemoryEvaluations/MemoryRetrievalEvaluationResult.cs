namespace MemorySystem.Application.MemoryEvaluations;

public sealed record MemoryRetrievalEvaluationResult(
    double Relevance,
    double Compactness,
    double WritePrecision,
    int RetrievalFalsePositiveCount,
    int DurableWriteFalsePositiveCount,
    double FalsePositiveRate,
    double ContradictionQuality,
    int RetrievedItemCount,
    IReadOnlyList<Guid> MissingRelevantSourceIds,
    IReadOnlyList<Guid> FalsePositiveSourceIds,
    IReadOnlyList<Guid> FalsePositiveWriteCandidateIds,
    IReadOnlyList<Guid> OversizedSourceIds,
    IReadOnlyList<Guid> RetrievedContradictedSourceIds)
{
    public int FalsePositiveCount => RetrievalFalsePositiveCount + DurableWriteFalsePositiveCount;

    public bool IsCompact => Compactness >= 1.0d;
}
