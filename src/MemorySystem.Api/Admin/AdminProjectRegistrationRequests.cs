namespace MemorySystem.Api.Admin;

public sealed record AdminProjectRegistrationRequest(
    Guid OrganizationId,
    string OrganizationName,
    Guid ProjectId,
    string ProjectName,
    string ProjectStatus,
    IReadOnlyList<AdminProjectRegistrationRoleDefinitionRequest>? RoleDefinitions,
    IReadOnlyList<AdminProjectRegistrationOwnerAssignmentRequest>? OwnerAssignments,
    IReadOnlyList<AdminProjectRegistrationNamespaceGrantRequest>? NamespaceGrants,
    IReadOnlyList<AdminProjectRegistrationSourceDocumentRequest>? SourceDocuments,
    string AccessPreviewReportId,
    string? AuditExportId,
    string? RegistrationNote);

public sealed record AdminProjectRegistrationRoleDefinitionRequest(
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string? Status);

public sealed record AdminProjectRegistrationOwnerAssignmentRequest(
    Guid PrincipalId,
    string RoleId,
    string ProjectAccessLevel,
    string? PrincipalLabel);

public sealed record AdminProjectRegistrationNamespaceGrantRequest(
    Guid? PrincipalId,
    string? RoleId,
    string NamespacePrefix,
    string Permission);

public sealed record AdminProjectRegistrationSourceDocumentRequest(
    string Path,
    string SourceContentSha256,
    string? SourceOwnerRoleId);
