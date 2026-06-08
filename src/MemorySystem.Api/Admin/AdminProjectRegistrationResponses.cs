namespace MemorySystem.Api.Admin;

public sealed record AdminProjectRegistrationResponse(
    string ContractId,
    string Status,
    AdminProjectRegistrationOrganizationResponse Organization,
    AdminProjectRegistrationProjectResponse Project,
    IReadOnlyList<AdminProjectRegistrationRoleDefinitionResponse> RoleDefinitions,
    IReadOnlyList<AdminProjectRegistrationOwnerAssignmentResponse> OwnerAssignments,
    IReadOnlyList<AdminProjectRegistrationNamespaceGrantResponse> NamespaceGrants,
    AdminProjectRegistrationAuditEvidenceResponse AuditEvidence,
    int SourceDocumentCount,
    int SourceHashCoveragePercent,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectRegistrationOrganizationResponse(
    Guid OrganizationId,
    string OrganizationName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminProjectRegistrationProjectResponse(
    Guid ProjectId,
    Guid OrganizationId,
    string ProjectName,
    string ProjectStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminProjectRegistrationRoleDefinitionResponse(
    Guid ProjectId,
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminProjectRegistrationOwnerAssignmentResponse(
    Guid PrincipalId,
    string RoleId,
    string ProjectAccessLevel,
    string? PrincipalLabel,
    Guid RoleAssignmentId,
    DateTimeOffset MembershipCreatedAt,
    DateTimeOffset RoleAssignedAt);

public sealed record AdminProjectRegistrationNamespaceGrantResponse(
    Guid GrantId,
    Guid? PrincipalId,
    string? RoleId,
    string NamespacePrefix,
    string Permission,
    DateTimeOffset CreatedAt);

public sealed record AdminProjectRegistrationAuditEvidenceResponse(
    Guid AuditEventId,
    DateTimeOffset OccurredAt,
    Guid IdempotencyRecordId,
    string RegistrationRequestHash,
    string AccessPreviewReportId,
    string? AuditExportId,
    string ActionType,
    string ResourceType,
    string ResourceId);
