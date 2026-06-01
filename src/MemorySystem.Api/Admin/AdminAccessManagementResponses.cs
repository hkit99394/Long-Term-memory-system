namespace MemorySystem.Api.Admin;

public sealed record AdminOrganizationMembershipResponse(
    Guid OrgId,
    Guid PrincipalId,
    string AccessLevel,
    DateTimeOffset CreatedAt);

public sealed record AdminProjectMembershipResponse(
    Guid ProjectId,
    Guid PrincipalId,
    string AccessLevel,
    DateTimeOffset CreatedAt);

public sealed record AdminRoleAssignmentResponse(
    Guid AssignmentId,
    Guid PrincipalId,
    string RoleId,
    string ScopeType,
    Guid ScopeId,
    DateTimeOffset CreatedAt);

public sealed record AdminNamespaceGrantResponse(
    Guid GrantId,
    Guid? PrincipalId,
    string? RoleId,
    string NamespacePrefix,
    string Permission,
    DateTimeOffset CreatedAt);

public sealed record AdminEffectiveAccessPreviewResponse(
    Guid PrincipalId,
    string Permission,
    AdminAccessScopeResponse Scope,
    string? NamespacePrefix,
    bool Allowed,
    string Reason,
    string EvaluatedBy);

public sealed record AdminAccessScopeResponse(
    string ScopeType,
    string ScopeId,
    Guid? OrgId,
    Guid? ProjectId,
    string? RoleId);
