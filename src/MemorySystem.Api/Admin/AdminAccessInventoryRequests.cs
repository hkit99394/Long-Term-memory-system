namespace MemorySystem.Api.Admin;

public sealed record AdminAccessRevocationRequest(
    string ScopeType,
    Guid ScopeId,
    string AccessRecordType,
    Guid? AccessRecordId,
    Guid? PrincipalId,
    string Reason,
    string AuditEvidenceId);
