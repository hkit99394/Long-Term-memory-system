namespace MemorySystem.Application.MemoryProposals;

public sealed record MemoryProposalDecision(
    string Decision,
    string Reason,
    Guid? MemoryId,
    Guid? SourceEventId,
    string CandidateKind = MemoryCandidateClassifications.Unsupported,
    decimal? Confidence = null);
