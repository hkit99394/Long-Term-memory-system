namespace MemorySystem.Api.Admin;

public sealed record AdminLegalHoldCreateResponse(
    Guid HoldId,
    string Status,
    int MatchedEvents,
    int NewlyHeldEvents,
    int AlreadyHeldEvents,
    string Reason,
    DateTimeOffset CreatedAt);

public sealed record AdminLegalHoldsResponse(
    IReadOnlyList<AdminLegalHoldResponse> Holds);

public sealed record AdminLegalHoldResponse(
    Guid Id,
    string Status,
    string Reason,
    string? ReleaseReason,
    Guid CreatedByPrincipalId,
    Guid? ReleasedByPrincipalId,
    string? ScopeType,
    string? ScopeId,
    string? NamespacePrefix,
    string? RetentionClass,
    string? Sensitivity,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReleasedAt,
    int EventCount,
    int ActiveEventCount,
    int ReleasedEventCount);

public sealed record AdminLegalHoldReleaseResponse(
    Guid HoldId,
    string Status,
    int ReleasedEvents,
    int RestoredEvents,
    DateTimeOffset? ReleasedAt);

public sealed record AdminErasureExecutionResponse(
    int MatchedEvents,
    int ErasedEvents,
    int HeldEvents,
    int RedactedFacts,
    int RedactedRoleLenses,
    int RedactedChunks,
    int StaleVaultExports,
    int ClearedReviewNotes,
    int RedactionRecords,
    Guid? AuditEventId,
    DateTimeOffset ExecutedAt);

public sealed record AdminRetentionReportResponse(
    IReadOnlyList<AdminRetentionReportRowResponse> Rows);

public sealed record AdminRetentionReportRowResponse(
    string Namespace,
    string RetentionClass,
    string Sensitivity,
    string AgeBucket,
    int EventCount,
    int LegalHoldEvents,
    int ErasureRequestedEvents,
    int RedactedEvents,
    DateTimeOffset OldestCreatedAt,
    DateTimeOffset NewestCreatedAt);
