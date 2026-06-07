namespace MemorySystem.Api.Admin;

public sealed record AdminOrganizationMembershipRequest(
    Guid OrgId,
    Guid PrincipalId,
    string AccessLevel);

public sealed record AdminProjectMembershipRequest(
    Guid ProjectId,
    Guid PrincipalId,
    string AccessLevel);

public sealed record AdminProjectRoleDefinitionRequest(
    Guid ProjectId,
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string? Status);

public sealed record AdminRoleAssignmentRequest(
    Guid PrincipalId,
    string RoleId,
    string ScopeType,
    Guid ScopeId);

public sealed record AdminNamespaceGrantRequest(
    Guid? PrincipalId,
    string? RoleId,
    string NamespacePrefix,
    string Permission,
    string ScopeType,
    Guid ScopeId);

public sealed record AdminEffectiveAccessPreviewRequest(
    Guid PrincipalId,
    string Permission,
    string ScopeType,
    string ScopeId,
    string? NamespacePrefix);

public sealed record AdminPermissionDriftReportRequest(
    string ScopeType,
    Guid ScopeId,
    string? NamespacePrefix,
    int? StaleAfterDays,
    int? MaxPreviewPrincipals);
