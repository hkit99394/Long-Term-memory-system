namespace MemorySystem.Api.MemoryFacts;

public sealed record MemoryQueryFactsResponse(
    string Query,
    MemoryQueryFactsTargetScopeResponse? TargetScope,
    string? RoleId,
    IReadOnlyList<MemoryQueryFactResponse> Facts,
    IReadOnlyList<MemoryQueryFactContradictionResponse> Contradictions,
    IReadOnlyList<MemoryQueryFactExclusionResponse> Excluded,
    IReadOnlyList<string> Warnings,
    decimal OverallConfidence);

public sealed record MemoryQueryFactsTargetScopeResponse(
    string ScopeType,
    string ScopeId);

public sealed record MemoryQueryFactResponse(
    Guid Id,
    string Claim,
    string MemoryType,
    string Status,
    decimal Confidence,
    string ScopeType,
    string ScopeId,
    string Namespace,
    IReadOnlyList<Guid> SourceEventIds,
    IReadOnlyList<string> SourceLinks,
    MemoryQueryFactPolicyResponse Policy);

public sealed record MemoryQueryFactPolicyResponse(
    bool Authorized,
    string TrustLevel,
    string Sensitivity,
    string LifecycleStatus,
    bool EvidenceCurrent);

public sealed record MemoryQueryFactContradictionResponse(
    string Subject,
    string Predicate,
    Guid CurrentFactId,
    Guid RelatedFactId,
    string RelatedStatus,
    string Summary,
    IReadOnlyList<Guid> SourceEventIds,
    IReadOnlyList<string> SourceLinks);

public sealed record MemoryQueryFactExclusionResponse(
    string Reason,
    int? Count,
    string? CountDisclosure);
