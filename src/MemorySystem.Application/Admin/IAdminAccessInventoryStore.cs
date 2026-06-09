namespace MemorySystem.Application.Admin;

public interface IAdminAccessInventoryStore
{
    Task<AdminAccessInventoryRecord?> GetOrganizationInventoryAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<AdminAccessInventoryRecord?> GetProjectInventoryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<AdminAccessRevocationRecord> RevokeAccessAsync(
        AdminAccessRevocationCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record AdminAccessRevocationCommand(
    Guid ActorPrincipalId,
    string ScopeType,
    Guid ScopeId,
    string AccessRecordType,
    Guid? AccessRecordId,
    Guid? PrincipalId,
    string Reason,
    string AuditEvidenceId,
    string? RequestMethod,
    string? RequestPath,
    string? CorrelationId);

public sealed record AdminAccessInventoryRecord(
    string ContractId,
    AdminAccessInventoryScopeRecord Scope,
    AdminAccessInventoryCountsRecord Counts,
    IReadOnlyList<AdminOrganizationMembershipInventoryRecord> OrganizationMemberships,
    IReadOnlyList<AdminProjectMembershipInventoryRecord> ProjectMemberships,
    IReadOnlyList<AdminRoleAssignmentInventoryRecord> RoleAssignments,
    IReadOnlyList<AdminNamespaceGrantInventoryRecord> NamespaceGrants,
    IReadOnlyList<AdminStaleAccessPromptRecord> StaleAccessPrompts,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminAccessRevocationRecord(
    string ContractId,
    string Status,
    AdminAccessInventoryScopeRecord Scope,
    AdminRevokedAccessRecord RevokedAccess,
    AdminProjectManagementAuditEvidenceRecord AuditEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminAccessInventoryScopeRecord(
    string ScopeType,
    Guid ScopeId,
    Guid OrganizationId,
    string OrganizationName,
    Guid? ProjectId,
    string? ProjectName,
    string? ProjectStatus);

public sealed record AdminAccessInventoryCountsRecord(
    int OrganizationMemberships,
    int ProjectMemberships,
    int RoleAssignments,
    int NamespaceGrants,
    int StaleAccessPrompts);

public sealed record AdminOrganizationMembershipInventoryRecord(
    string AccessRecordType,
    string AccessRecordId,
    Guid OrganizationId,
    string OrganizationName,
    Guid PrincipalId,
    string PrincipalDisplayName,
    string PrincipalStatus,
    string AccessLevel,
    DateTimeOffset CreatedAt,
    string? ReviewPrompt);

public sealed record AdminProjectMembershipInventoryRecord(
    string AccessRecordType,
    string AccessRecordId,
    Guid ProjectId,
    string ProjectName,
    string ProjectStatus,
    Guid PrincipalId,
    string PrincipalDisplayName,
    string PrincipalStatus,
    string AccessLevel,
    DateTimeOffset CreatedAt,
    string? ReviewPrompt);

public sealed record AdminRoleAssignmentInventoryRecord(
    string AccessRecordType,
    Guid AccessRecordId,
    string ScopeType,
    Guid ScopeId,
    string ScopeName,
    string? ProjectStatus,
    Guid PrincipalId,
    string PrincipalDisplayName,
    string PrincipalStatus,
    string RoleId,
    DateTimeOffset CreatedAt,
    string? ReviewPrompt);

public sealed record AdminNamespaceGrantInventoryRecord(
    string AccessRecordType,
    Guid AccessRecordId,
    string ScopeType,
    Guid ScopeId,
    string ScopeName,
    string? ProjectStatus,
    Guid? PrincipalId,
    string? PrincipalDisplayName,
    string? PrincipalStatus,
    string? RoleId,
    string NamespacePrefix,
    string Permission,
    DateTimeOffset CreatedAt,
    string? ReviewPrompt);

public sealed record AdminStaleAccessPromptRecord(
    string AccessRecordType,
    string AccessRecordId,
    string Severity,
    string Prompt);

public sealed record AdminRevokedAccessRecord(
    string AccessRecordType,
    string AccessRecordId,
    Guid? PrincipalId,
    string? PrincipalDisplayName,
    string? RoleId,
    string? NamespacePrefix,
    string? Permission,
    string Reason,
    string AuditEvidenceId);
