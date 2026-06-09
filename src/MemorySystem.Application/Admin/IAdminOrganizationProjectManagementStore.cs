namespace MemorySystem.Application.Admin;

public interface IAdminOrganizationProjectManagementStore
{
    Task<AdminOrganizationListRecord> ListOrganizationsAsync(
        AdminOrganizationListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminOrganizationDetailRecord?> GetOrganizationAsync(
        AdminOrganizationDetailQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminProjectListRecord> ListProjectsAsync(
        AdminProjectListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminProjectDetailRecord?> GetProjectAsync(
        AdminProjectDetailQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record AdminOrganizationListQuery(
    Guid ActorPrincipalId,
    string? SearchText,
    int Limit,
    int Offset);

public sealed record AdminOrganizationDetailQuery(
    Guid ActorPrincipalId,
    Guid OrganizationId);

public sealed record AdminProjectListQuery(
    Guid ActorPrincipalId,
    Guid? OrganizationId,
    string? ProjectStatus,
    string? SearchText,
    int Limit,
    int Offset);

public sealed record AdminProjectDetailQuery(
    Guid ActorPrincipalId,
    Guid ProjectId);

public sealed record AdminOrganizationListRecord(
    IReadOnlyList<AdminOrganizationSummaryRecord> Organizations,
    string? NextCursor);

public sealed record AdminOrganizationDetailRecord(
    AdminOrganizationSummaryRecord Organization);

public sealed record AdminProjectListRecord(
    IReadOnlyList<AdminProjectSummaryRecord> Projects,
    string? NextCursor);

public sealed record AdminProjectDetailRecord(
    AdminProjectSummaryRecord Project,
    AdminProjectRegistrationEvidenceRecord? LatestRegistrationEvidence);

public sealed record AdminOrganizationSummaryRecord(
    Guid OrganizationId,
    string OrganizationName,
    string ActorAccessLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    AdminProjectStatusCountsRecord ProjectStatusCounts,
    int ProjectCount,
    int OrganizationMembershipCount,
    int ProjectMembershipCount,
    int RoleAssignmentCount,
    int NamespaceGrantCount);

public sealed record AdminProjectSummaryRecord(
    Guid ProjectId,
    Guid OrganizationId,
    string OrganizationName,
    string ProjectName,
    string ProjectStatus,
    string ActorAccessLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int ProjectMembershipCount,
    int RoleDefinitionCount,
    int ActiveRoleDefinitionCount,
    int RoleAssignmentCount,
    int NamespaceGrantCount);

public sealed record AdminProjectStatusCountsRecord(
    int Planned,
    int Active,
    int Archived,
    int Deleted);

public sealed record AdminProjectRegistrationEvidenceRecord(
    Guid AuditEventId,
    DateTimeOffset OccurredAt,
    Guid? IdempotencyRecordId,
    string? RegistrationRequestHash,
    string? AccessPreviewReportId,
    string? AuditExportId,
    int SourceDocumentCount,
    int SourceHashCoveragePercent,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);
