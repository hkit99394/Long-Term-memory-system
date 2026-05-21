namespace MemorySystem.Application.MemoryProposals;

public sealed record MemoryProposalCommand(
    Guid? SourceEventId,
    bool SourceEventExists,
    string MemoryType,
    string ScopeType,
    string ScopeId,
    string Namespace,
    string Visibility,
    string Subject,
    string Predicate,
    string Object,
    decimal? Confidence,
    string TrustLevel,
    string Sensitivity);
