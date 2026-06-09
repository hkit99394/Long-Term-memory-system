namespace MemorySystem.Application.Admin;

public interface IAdminManagementActivityStore
{
    Task<AdminManagementActivityRecord?> GetOrganizationActivityAsync(
        AdminManagementActivityQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminManagementActivityRecord?> GetProjectActivityAsync(
        AdminManagementActivityQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record AdminManagementActivityQuery(
    Guid ActorPrincipalId,
    Guid ScopeId,
    int Limit,
    int Offset);

public sealed record AdminManagementActivityRecord(
    string ContractId,
    AdminManagementActivityScopeRecord Scope,
    IReadOnlyList<AdminManagementActivityEntryRecord> Entries,
    int ReturnedCount,
    int Limit,
    string? NextCursor,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminManagementActivityScopeRecord(
    string ScopeType,
    Guid ScopeId,
    Guid OrganizationId,
    string OrganizationName,
    Guid? ProjectId,
    string? ProjectName,
    string? ProjectStatus);

public sealed record AdminManagementActivityEntryRecord(
    Guid AuditEventId,
    DateTimeOffset OccurredAt,
    Guid? ActorPrincipalId,
    Guid? TargetPrincipalId,
    string ActionType,
    string Outcome,
    string? ScopeType,
    string? ScopeId,
    string? RoleId,
    string? NamespacePrefix,
    string? Permission,
    string? ResourceType,
    string? ResourceId,
    string? RequestMethod,
    string? RequestPath,
    string? CorrelationId,
    string? Operation,
    string? SourceContractId,
    string? AuditEvidenceId,
    string Summary,
    IReadOnlyList<AdminManagementActivityMetadataRecord> Metadata);

public sealed record AdminManagementActivityMetadataRecord(
    string Key,
    string Value);
