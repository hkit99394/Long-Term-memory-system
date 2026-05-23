namespace MemorySystem.Api.MemoryFacts;

public sealed record MemoryFactResponse(
    Guid Id,
    string ScopeType,
    string ScopeId,
    string Namespace,
    string MemoryType,
    string Visibility,
    string Subject,
    string Predicate,
    string Object,
    decimal Confidence,
    string TrustLevel,
    string Status,
    Guid SourceEventId,
    Guid? ProposedByPrincipalId);
