namespace MemorySystem.Api.Admin;

public sealed record AdminProjectLifecycleUpdateRequest(
    string ProjectStatus,
    string Reason,
    string AuditEvidenceId);

public sealed record AdminProjectScopeSettingsUpdateRequest(
    string DefaultNamespacePrefix,
    bool SourceHashRequired,
    string MemoryRetentionClass,
    int ReviewCadenceDays,
    string Reason,
    string AuditEvidenceId);
