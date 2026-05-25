namespace MemorySystem.Application.MemoryEvaluations;

public sealed record MemoryRetrievalWriteObservation(
    Guid CandidateId,
    bool ExpectedDurable,
    bool StoredDurably,
    bool ExpectedContradiction = false,
    bool RoutedAsContradiction = false);
