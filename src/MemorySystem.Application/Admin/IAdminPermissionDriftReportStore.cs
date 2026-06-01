namespace MemorySystem.Application.Admin;

public interface IAdminPermissionDriftReportStore
{
    Task<AdminPermissionDriftReport> GenerateAsync(
        AdminPermissionDriftReportQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record AdminPermissionDriftReportQuery(
    Guid ActorPrincipalId,
    string ScopeType,
    Guid ScopeId,
    string? NamespacePrefix,
    int StaleAfterDays,
    int MaxPreviewPrincipals);

public sealed record AdminPermissionDriftReport(
    Guid ReportId,
    DateTimeOffset GeneratedAt,
    AdminPermissionDriftScope Scope,
    string NamespacePrefix,
    int StaleAfterDays,
    IReadOnlyList<AdminPermissionDriftPrincipalRecord> Principals,
    IReadOnlyList<AdminPermissionDriftIdentityBindingRecord> IdentityBindings,
    IReadOnlyList<AdminPermissionDriftServiceAccountRecord> ServiceAccounts,
    IReadOnlyList<AdminPermissionDriftServiceCredentialRecord> ServiceCredentials,
    IReadOnlyList<AdminPermissionDriftOrganizationMembershipRecord> OrganizationMemberships,
    IReadOnlyList<AdminPermissionDriftProjectMembershipRecord> ProjectMemberships,
    IReadOnlyList<AdminPermissionDriftRoleAssignmentRecord> RoleAssignments,
    IReadOnlyList<AdminPermissionDriftNamespaceGrantRecord> NamespaceGrants,
    IReadOnlyList<AdminPermissionDriftEffectiveAccessPreviewRecord> EffectiveAccessPreviews,
    IReadOnlyList<AdminPermissionDriftFindingRecord> Findings);

public sealed record AdminPermissionDriftScope(
    string ScopeType,
    Guid ScopeId,
    Guid? OrgId,
    Guid? ProjectId);

public sealed record AdminPermissionDriftPrincipalRecord(
    Guid PrincipalId,
    string PrincipalType,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminPermissionDriftIdentityBindingRecord(
    Guid BindingId,
    Guid PrincipalId,
    string Provider,
    string IssuerHash,
    string Status,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminPermissionDriftServiceAccountRecord(
    Guid ServicePrincipalId,
    string OwnerScopeType,
    Guid OwnerScopeId,
    Guid? OwnerPrincipalId,
    string AllowedAuthMethod,
    string Status,
    DateTimeOffset? ReviewDueAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminPermissionDriftServiceCredentialRecord(
    Guid CredentialId,
    Guid ServicePrincipalId,
    string AuthMethod,
    string Status,
    DateTimeOffset? ReviewDueAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminPermissionDriftOrganizationMembershipRecord(
    Guid OrgId,
    Guid PrincipalId,
    string AccessLevel,
    DateTimeOffset CreatedAt);

public sealed record AdminPermissionDriftProjectMembershipRecord(
    Guid ProjectId,
    Guid OrgId,
    Guid PrincipalId,
    string AccessLevel,
    DateTimeOffset CreatedAt);

public sealed record AdminPermissionDriftRoleAssignmentRecord(
    Guid AssignmentId,
    Guid PrincipalId,
    string RoleId,
    string ScopeType,
    Guid? ScopeId,
    DateTimeOffset CreatedAt);

public sealed record AdminPermissionDriftNamespaceGrantRecord(
    Guid GrantId,
    Guid? PrincipalId,
    string? RoleId,
    string NamespacePrefix,
    string Permission,
    DateTimeOffset CreatedAt);

public sealed record AdminPermissionDriftEffectiveAccessPreviewRecord(
    Guid PrincipalId,
    string Permission,
    string ScopeType,
    Guid ScopeId,
    string NamespacePrefix,
    bool Allowed,
    string Reason,
    string EvaluatedBy);

public sealed record AdminPermissionDriftFindingRecord(
    string Severity,
    string Code,
    string ResourceType,
    string ResourceId,
    Guid? PrincipalId,
    string? ScopeType,
    string? ScopeId,
    string? NamespacePrefix,
    string Detail,
    string RecommendedAction);
