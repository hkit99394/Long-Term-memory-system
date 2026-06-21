namespace MemorySystem.Application.Admin;

public interface IAdminGovernanceStore
{
    Task<AdminLegalHoldResult> CreateLegalHoldAsync(
        AdminLegalHoldCreateCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminLegalHoldReleaseResult> ReleaseLegalHoldAsync(
        AdminLegalHoldReleaseCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminLegalHoldRecord>> ListLegalHoldsAsync(
        AdminLegalHoldListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminErasureExecutionResult> ExecuteErasureAsync(
        AdminErasureExecutionCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminRetentionReportRecord>> ReadRetentionReportAsync(
        AdminRetentionReportQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record AdminGovernanceEventSelector(
    Guid PrincipalId,
    int MaxEvents,
    IReadOnlyList<Guid> EventIds,
    string? ScopeType = null,
    string? ScopeId = null,
    string? NamespacePrefix = null,
    string? RetentionClass = null,
    string? Sensitivity = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null);

public sealed record AdminLegalHoldCreateCommand(
    Guid PrincipalId,
    Guid IdempotencyRecordId,
    string RequestHash,
    string Reason,
    AdminGovernanceEventSelector Selector);

public sealed record AdminLegalHoldResult(
    Guid HoldId,
    string Status,
    int MatchedEvents,
    int NewlyHeldEvents,
    int AlreadyHeldEvents,
    string Reason,
    DateTimeOffset CreatedAt);

public sealed record AdminLegalHoldReleaseCommand(
    Guid PrincipalId,
    Guid IdempotencyRecordId,
    string RequestHash,
    Guid HoldId,
    string Reason);

public sealed record AdminLegalHoldReleaseResult(
    bool Succeeded,
    int FailureStatusCode,
    string? Error,
    Guid HoldId,
    string Status,
    int ReleasedEvents,
    int RestoredEvents,
    DateTimeOffset? ReleasedAt)
{
    public static AdminLegalHoldReleaseResult NotFound(Guid holdId)
    {
        return new AdminLegalHoldReleaseResult(
            false,
            404,
            "The legal hold does not exist.",
            holdId,
            "not_found",
            ReleasedEvents: 0,
            RestoredEvents: 0,
            ReleasedAt: null);
    }

    public static AdminLegalHoldReleaseResult Forbidden(Guid holdId)
    {
        return new AdminLegalHoldReleaseResult(
            false,
            403,
            "The authenticated principal is not allowed to release every event in this legal hold.",
            holdId,
            "forbidden",
            ReleasedEvents: 0,
            RestoredEvents: 0,
            ReleasedAt: null);
    }
}

public sealed record AdminLegalHoldListQuery(
    Guid PrincipalId,
    int Limit,
    string? Status = null);

public sealed record AdminLegalHoldRecord(
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

public sealed record AdminErasureExecutionCommand(
    Guid PrincipalId,
    Guid IdempotencyRecordId,
    string RequestHash,
    string Reason,
    AdminGovernanceEventSelector Selector);

public sealed record AdminErasureExecutionResult(
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

public sealed record AdminRetentionReportQuery(
    Guid PrincipalId,
    int Limit,
    string? ScopeType = null,
    string? ScopeId = null,
    string? NamespacePrefix = null);

public sealed record AdminRetentionReportRecord(
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
