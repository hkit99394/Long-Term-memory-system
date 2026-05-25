namespace MemorySystem.Application.MemoryEvaluations;

public sealed record MemoryRetrievalEvaluationCase(
    IReadOnlyCollection<Guid> RelevantSourceIds,
    IReadOnlyCollection<Guid> AllowedSourceIds,
    IReadOnlyCollection<Guid> ForbiddenSourceIds,
    IReadOnlyCollection<Guid> ContradictedSourceIds,
    int MaxItemCount,
    int MaxContentLength,
    IReadOnlyCollection<MemoryRetrievalWriteObservation> WriteObservations);
