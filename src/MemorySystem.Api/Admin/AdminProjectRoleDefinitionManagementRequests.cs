namespace MemorySystem.Api.Admin;

public sealed record AdminProjectRoleDefinitionManagementUpdateRequest(
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string Status,
    string Reason,
    string AuditEvidenceId);
