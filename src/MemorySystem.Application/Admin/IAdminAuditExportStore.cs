namespace MemorySystem.Application.Admin;

public interface IAdminAuditExportStore
{
    Task<IReadOnlyList<AdminAuditExportEventRecord>> ListAccessAuditEventsAsync(
        AdminAuditExportQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record AdminAuditExportQuery(
    DateTimeOffset OccurredFrom,
    DateTimeOffset OccurredTo,
    string ScopeType,
    string ScopeId,
    int Limit,
    IReadOnlyList<string> ActionTypes,
    IReadOnlyList<string> Outcomes);

public sealed record AdminAuditExportEventRecord(
    Guid Id,
    string ActionType,
    string Outcome,
    Guid? ActorPrincipalId,
    Guid? TargetPrincipalId,
    string? PrincipalType,
    string? AuthMethod,
    string? CredentialId,
    Guid? IdentityBindingId,
    string? ScopeType,
    string? ScopeId,
    string? RoleId,
    string? NamespacePrefix,
    string? Permission,
    string? ResourceType,
    string? ResourceId,
    string? ReasonCode,
    string? RequestMethod,
    string? RequestPath,
    string? CorrelationId,
    IReadOnlyDictionary<string, string?> AuditMetadata,
    DateTimeOffset OccurredAt);
