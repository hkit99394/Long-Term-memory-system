namespace MemorySystem.Application.Admin;

public interface IAdminProjectRegistrationStore
{
    Task<AdminProjectRegistrationRecord> RegisterAsync(
        AdminProjectRegistrationCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record AdminProjectRegistrationCommand(
    Guid ActorPrincipalId,
    Guid IdempotencyRecordId,
    string RegistrationRequestHash,
    Guid OrganizationId,
    string OrganizationName,
    Guid ProjectId,
    string ProjectName,
    string ProjectStatus,
    IReadOnlyList<AdminProjectRegistrationRoleDefinitionCommand> RoleDefinitions,
    IReadOnlyList<AdminProjectRegistrationOwnerAssignmentCommand> OwnerAssignments,
    IReadOnlyList<AdminProjectRegistrationNamespaceGrantCommand> NamespaceGrants,
    IReadOnlyList<AdminProjectRegistrationSourceDocumentCommand> SourceDocuments,
    string AccessPreviewReportId,
    string? AuditExportId,
    string? RegistrationNote,
    string? RequestMethod,
    string? RequestPath,
    string? CorrelationId);

public sealed record AdminProjectRegistrationRoleDefinitionCommand(
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string? Status);

public sealed record AdminProjectRegistrationOwnerAssignmentCommand(
    Guid PrincipalId,
    string RoleId,
    string ProjectAccessLevel,
    string? PrincipalLabel);

public sealed record AdminProjectRegistrationNamespaceGrantCommand(
    Guid? PrincipalId,
    string? RoleId,
    string NamespacePrefix,
    string Permission);

public sealed record AdminProjectRegistrationSourceDocumentCommand(
    string Path,
    string SourceContentSha256,
    string? SourceOwnerRoleId);

public sealed record AdminProjectRegistrationRecord(
    string ContractId,
    string Status,
    AdminProjectRegistrationOrganizationRecord Organization,
    AdminProjectRegistrationProjectRecord Project,
    IReadOnlyList<AdminProjectRegistrationRoleDefinitionRecord> RoleDefinitions,
    IReadOnlyList<AdminProjectRegistrationOwnerAssignmentRecord> OwnerAssignments,
    IReadOnlyList<AdminProjectRegistrationNamespaceGrantRecord> NamespaceGrants,
    AdminProjectRegistrationAuditEvidenceRecord AuditEvidence,
    int SourceDocumentCount,
    int SourceHashCoveragePercent,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectRegistrationOrganizationRecord(
    Guid OrganizationId,
    string OrganizationName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminProjectRegistrationProjectRecord(
    Guid ProjectId,
    Guid OrganizationId,
    string ProjectName,
    string ProjectStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminProjectRegistrationRoleDefinitionRecord(
    Guid ProjectId,
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminProjectRegistrationOwnerAssignmentRecord(
    Guid PrincipalId,
    string RoleId,
    string ProjectAccessLevel,
    string? PrincipalLabel,
    Guid RoleAssignmentId,
    DateTimeOffset MembershipCreatedAt,
    DateTimeOffset RoleAssignedAt);

public sealed record AdminProjectRegistrationNamespaceGrantRecord(
    Guid GrantId,
    Guid? PrincipalId,
    string? RoleId,
    string NamespacePrefix,
    string Permission,
    DateTimeOffset CreatedAt);

public sealed record AdminProjectRegistrationAuditEvidenceRecord(
    Guid AuditEventId,
    DateTimeOffset OccurredAt,
    Guid IdempotencyRecordId,
    string RegistrationRequestHash,
    string AccessPreviewReportId,
    string? AuditExportId,
    string ActionType,
    string ResourceType,
    string ResourceId);
