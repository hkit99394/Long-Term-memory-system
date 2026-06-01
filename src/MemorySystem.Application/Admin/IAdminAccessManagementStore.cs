namespace MemorySystem.Application.Admin;

public interface IAdminAccessManagementStore
{
    Task<AdminOrganizationMembershipRecord> UpsertOrganizationMembershipAsync(
        AdminOrganizationMembershipCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminProjectMembershipRecord> UpsertProjectMembershipAsync(
        AdminProjectMembershipCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminRoleAssignmentRecord> UpsertRoleAssignmentAsync(
        AdminRoleAssignmentCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminNamespaceGrantRecord> UpsertNamespaceGrantAsync(
        AdminNamespaceGrantCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record AdminOrganizationMembershipCommand(
    Guid ActorPrincipalId,
    Guid OrgId,
    Guid PrincipalId,
    string AccessLevel);

public sealed record AdminProjectMembershipCommand(
    Guid ActorPrincipalId,
    Guid ProjectId,
    Guid PrincipalId,
    string AccessLevel);

public sealed record AdminRoleAssignmentCommand(
    Guid ActorPrincipalId,
    Guid PrincipalId,
    string RoleId,
    string ScopeType,
    Guid ScopeId);

public sealed record AdminNamespaceGrantCommand(
    Guid ActorPrincipalId,
    Guid? PrincipalId,
    string? RoleId,
    string ScopeType,
    Guid ScopeId,
    string NamespacePrefix,
    string Permission);

public sealed record AdminOrganizationMembershipRecord(
    Guid OrgId,
    Guid PrincipalId,
    string AccessLevel,
    DateTimeOffset CreatedAt);

public sealed record AdminProjectMembershipRecord(
    Guid ProjectId,
    Guid PrincipalId,
    string AccessLevel,
    DateTimeOffset CreatedAt);

public sealed record AdminRoleAssignmentRecord(
    Guid AssignmentId,
    Guid PrincipalId,
    string RoleId,
    string ScopeType,
    Guid ScopeId,
    DateTimeOffset CreatedAt);

public sealed record AdminNamespaceGrantRecord(
    Guid GrantId,
    Guid? PrincipalId,
    string? RoleId,
    string NamespacePrefix,
    string Permission,
    DateTimeOffset CreatedAt);
