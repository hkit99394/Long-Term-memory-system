namespace MemorySystem.Application.MemoryProposals;

public sealed record MemoryProposalWorkflowRequest(
    Guid AuthenticatedPrincipalId,
    Guid IdempotencyRecordId,
    string RequestHash,
    Guid? SourceEventId,
    string? MemoryType,
    string? ScopeType,
    string? ScopeId,
    string? Namespace,
    string? Visibility,
    string? Subject,
    string? Predicate,
    string? Object,
    decimal? Confidence,
    string? TrustLevel,
    string? Sensitivity);
