namespace MemorySystem.Api.Admin;

public sealed record AdminGrantMatrixUpdateRequest(
    string RoleId,
    string? PresetId,
    IReadOnlyList<AdminGrantMatrixGrantRequest> Grants,
    string Reason,
    string AuditEvidenceId);

public sealed record AdminGrantMatrixGrantRequest(
    string NamespacePrefix,
    string Permission);
