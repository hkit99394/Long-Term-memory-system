namespace MemorySystem.Application.Admin;

public interface IAdminGrantMatrixStore
{
    Task<AdminProjectManagementContextRecord?> GetProjectContextAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<AdminGrantMatrixRecord?> GetProjectGrantMatrixAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<AdminGrantMatrixUpdateRecord> ReplaceRoleGrantsAsync(
        AdminGrantMatrixReplaceCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record AdminGrantMatrixReplaceCommand(
    Guid ActorPrincipalId,
    Guid ProjectId,
    string RoleId,
    string? PresetId,
    IReadOnlyList<AdminGrantMatrixGrantCommand> Grants,
    string Reason,
    string AuditEvidenceId,
    string? RequestMethod,
    string? RequestPath,
    string? CorrelationId);

public sealed record AdminGrantMatrixGrantCommand(
    string NamespacePrefix,
    string Permission);

public sealed record AdminGrantMatrixRecord(
    string ContractId,
    AdminProjectManagementContextRecord Project,
    IReadOnlyList<AdminGrantMatrixPresetRecord> Presets,
    IReadOnlyList<AdminGrantMatrixRoleRecord> Roles,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminGrantMatrixUpdateRecord(
    string ContractId,
    string Status,
    AdminProjectManagementContextRecord Project,
    AdminGrantMatrixRoleRecord Role,
    AdminProjectManagementAuditEvidenceRecord AuditEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminGrantMatrixPresetRecord(
    string PresetId,
    string DisplayName,
    string Description,
    IReadOnlyList<AdminGrantMatrixPresetGrantRecord> Grants);

public sealed record AdminGrantMatrixPresetGrantRecord(
    string NamespaceTemplate,
    string Permission);

public sealed record AdminGrantMatrixRoleRecord(
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string Status,
    string RecommendedPresetId,
    string PresetAlignment,
    IReadOnlyList<AdminGrantMatrixGrantRecord> Grants,
    IReadOnlyList<AdminGrantMatrixEffectivePreviewRecord> EffectiveAccessPreviews);

public sealed record AdminGrantMatrixGrantRecord(
    Guid? GrantId,
    string NamespacePrefix,
    string Permission,
    DateTimeOffset? CreatedAt,
    bool FromPreset);

public sealed record AdminGrantMatrixEffectivePreviewRecord(
    Guid PrincipalId,
    string RoleId,
    string Permission,
    string NamespacePrefix,
    bool Allowed,
    string Reason,
    string EvaluatedBy);
