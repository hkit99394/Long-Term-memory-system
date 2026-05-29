namespace MemorySystem.Application.MemoryFacts;

public sealed record MemoryFactFindingResult(
    string Query,
    MemoryFactFindingTargetScope? TargetScope,
    string? RoleId,
    IReadOnlyList<MemoryFactFindingFact> Facts,
    IReadOnlyList<MemoryFactFindingContradiction> Contradictions,
    IReadOnlyList<MemoryFactExclusionSummary> Excluded,
    IReadOnlyList<string> Warnings,
    decimal OverallConfidence);

public sealed record MemoryFactFindingTargetScope(
    string ScopeType,
    string ScopeId);

public sealed record MemoryFactFindingFact(
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
    MemoryFactFindingPolicy Policy);

public sealed record MemoryFactFindingPolicy(
    bool Authorized,
    string TrustLevel,
    string Sensitivity,
    string LifecycleStatus,
    bool EvidenceCurrent);

public sealed record MemoryFactFindingContradiction(
    string Subject,
    string Predicate,
    Guid CurrentFactId,
    Guid RelatedFactId,
    string RelatedStatus,
    string Summary,
    IReadOnlyList<Guid> SourceEventIds,
    IReadOnlyList<string> SourceLinks);

public sealed record MemoryFactExclusionSummary(
    string Reason,
    int? Count,
    string? CountDisclosure = null);

public sealed record MemoryFactFindingStoreResult(
    IReadOnlyList<MemoryFactFindingRecord> Facts,
    IReadOnlyList<MemoryFactContradictionRecord> Contradictions,
    IReadOnlyList<MemoryFactExclusionSummary> Exclusions);

public sealed record MemoryFactFindingRecord(
    Guid Id,
    string MemoryType,
    string Status,
    string ScopeType,
    string ScopeId,
    string Namespace,
    string Subject,
    string Predicate,
    string Object,
    decimal Confidence,
    string TrustLevel,
    string Sensitivity,
    string RetentionClass,
    string RedactionStatus,
    Guid SourceEventId);

public sealed record MemoryFactContradictionRecord(
    string Subject,
    string Predicate,
    Guid CurrentFactId,
    Guid RelatedFactId,
    string RelatedStatus,
    Guid SourceEventId);
