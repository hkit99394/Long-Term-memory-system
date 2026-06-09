namespace MemorySystem.Application.Admin;

public interface IAdminProjectLifecycleSettingsStore
{
    Task<AdminProjectManagementContextRecord?> GetProjectContextAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<AdminProjectScopeSettingsRecord> GetScopeSettingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<AdminProjectLifecycleUpdateRecord> UpdateLifecycleAsync(
        AdminProjectLifecycleUpdateCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminProjectScopeSettingsUpdateRecord> UpdateScopeSettingsAsync(
        AdminProjectScopeSettingsUpdateCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record AdminProjectManagementContextRecord(
    Guid ProjectId,
    Guid OrganizationId,
    string ProjectName,
    string ProjectStatus);

public sealed record AdminProjectLifecycleUpdateCommand(
    Guid ActorPrincipalId,
    Guid ProjectId,
    string ProjectStatus,
    string Reason,
    string AuditEvidenceId,
    string? RequestMethod,
    string? RequestPath,
    string? CorrelationId);

public sealed record AdminProjectScopeSettingsUpdateCommand(
    Guid ActorPrincipalId,
    Guid ProjectId,
    string DefaultNamespacePrefix,
    bool SourceHashRequired,
    string MemoryRetentionClass,
    int ReviewCadenceDays,
    string Reason,
    string AuditEvidenceId,
    string? RequestMethod,
    string? RequestPath,
    string? CorrelationId);

public sealed record AdminProjectLifecycleUpdateRecord(
    string ContractId,
    string Status,
    AdminProjectManagementContextRecord Project,
    string PreviousProjectStatus,
    AdminProjectManagementAuditEvidenceRecord AuditEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectScopeSettingsUpdateRecord(
    string ContractId,
    string Status,
    AdminProjectManagementContextRecord Project,
    AdminProjectScopeSettingsRecord ScopeSettings,
    AdminProjectManagementAuditEvidenceRecord AuditEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectScopeSettingsRecord(
    Guid ProjectId,
    string DefaultNamespacePrefix,
    bool SourceHashRequired,
    string MemoryRetentionClass,
    int ReviewCadenceDays,
    Guid? UpdatedByPrincipalId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDefault);

public sealed record AdminProjectManagementAuditEvidenceRecord(
    Guid AuditEventId,
    DateTimeOffset OccurredAt,
    string ActionType,
    string ResourceType,
    string ResourceId,
    string AuditEvidenceId);
