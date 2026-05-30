namespace MemorySystem.Application.Admin;

public interface IAdminMemoryInspectionStore
{
    Task<IReadOnlyList<AdminMemoryFactRecord>> ListMemoryFactsAsync(
        AdminMemoryFactListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminSourceEventRecord>> ListSourceEventsAsync(
        AdminSourceEventListQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record AdminMemoryFactListQuery(
    Guid PrincipalId,
    int Limit,
    string? Status = null,
    string? ScopeType = null,
    string? ScopeId = null,
    string? MemoryType = null,
    string? NamespacePrefix = null,
    string? Query = null);

public sealed record AdminMemoryFactRecord(
    Guid Id,
    string ScopeType,
    string ScopeId,
    string Namespace,
    Guid? UserPrincipalId,
    Guid? ProjectId,
    Guid? OrgId,
    string? RoleId,
    Guid? AgentPrincipalId,
    string MemoryType,
    string Visibility,
    string? Subject,
    string? Predicate,
    string? Object,
    decimal Confidence,
    string TrustLevel,
    string Status,
    Guid SourceEventId,
    Guid? ProposedByPrincipalId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool ContentVisible,
    string? ContentVisibilityReason,
    AdminMemorySourcePolicyRecord SourcePolicy);

public sealed record AdminMemorySourcePolicyRecord(
    string RetentionClass,
    string Sensitivity,
    string TrustLevel,
    string RedactionStatus,
    bool SourcePayloadIncluded);

public sealed record AdminSourceEventListQuery(
    Guid PrincipalId,
    int Limit,
    string? ScopeType = null,
    string? ScopeId = null,
    string? EventType = null,
    string? RetentionClass = null,
    string? Sensitivity = null,
    string? TrustLevel = null,
    string? RedactionStatus = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    string? Query = null);

public sealed record AdminSourceEventRecord(
    Guid Id,
    Guid? PrincipalId,
    Guid? ConversationId,
    Guid? AgentPrincipalId,
    string? RoleId,
    string EventType,
    string? ContentHash,
    string? ExternalPayloadUri,
    string RetentionClass,
    string Sensitivity,
    string RedactionStatus,
    DateTimeOffset? RedactedAt,
    Guid? RedactionEventId,
    string TrustLevel,
    DateTimeOffset CreatedAt,
    string ScopeType,
    string ScopeId,
    Guid? ScopeOrgId,
    Guid? ScopeProjectId,
    Guid? ScopePrincipalId,
    string? ScopeRoleId,
    bool SourcePayloadIncluded,
    string ContentVisibilityReason,
    IReadOnlyList<AdminSourceEventReferenceRecord> References);

public sealed record AdminSourceEventReferenceRecord(
    string ReferenceType,
    Guid Id,
    string Status,
    string? TargetType,
    Guid? TargetId,
    string? Label);
