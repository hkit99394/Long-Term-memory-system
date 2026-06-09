namespace MemorySystem.Application.Admin;

public interface IAdminProjectRoleDefinitionManagementStore
{
    Task<AdminProjectManagementContextRecord?> GetProjectContextAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<AdminProjectRoleDefinitionListRecord?> GetProjectRoleDefinitionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<AdminProjectRoleDefinitionUpdateRecord> UpsertProjectRoleDefinitionAsync(
        AdminProjectRoleDefinitionUpdateCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record AdminProjectRoleDefinitionUpdateCommand(
    Guid ActorPrincipalId,
    Guid ProjectId,
    string RouteRoleId,
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string Status,
    string Reason,
    string AuditEvidenceId,
    string? RequestMethod,
    string? RequestPath,
    string? CorrelationId);

public sealed record AdminProjectRoleDefinitionListRecord(
    string ContractId,
    AdminProjectManagementContextRecord Project,
    IReadOnlyList<AdminProjectRoleDefinitionRecord> Roles,
    int ActiveCount,
    int DisabledCount,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectRoleDefinitionUpdateRecord(
    string ContractId,
    string Status,
    AdminProjectManagementContextRecord Project,
    AdminProjectRoleDefinitionRecord Role,
    AdminProjectManagementAuditEvidenceRecord AuditEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectRoleDefinitionRecord(
    Guid ProjectId,
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string Status,
    int AssignmentCount,
    int RoleGrantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
