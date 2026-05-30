namespace MemorySystem.Application.Admin;

public interface IAdminMemoryInspectionStore
{
    Task<IReadOnlyList<AdminMemoryFactRecord>> ListMemoryFactsAsync(
        AdminMemoryFactListQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record AdminMemoryFactListQuery(
    Guid PrincipalId,
    int Limit,
    string? Status = null,
    string? ScopeType = null,
    string? ScopeId = null,
    string? MemoryType = null,
    string? NamespacePrefix = null,
    string? Query = null);

public sealed record AdminMemoryFactRecord(
    Guid Id,
    string ScopeType,
    string ScopeId,
    string Namespace,
    Guid? UserPrincipalId,
    Guid? ProjectId,
    Guid? OrgId,
    string? RoleId,
    Guid? AgentPrincipalId,
    string MemoryType,
    string Visibility,
    string? Subject,
    string? Predicate,
    string? Object,
    decimal Confidence,
    string TrustLevel,
    string Status,
    Guid SourceEventId,
    Guid? ProposedByPrincipalId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool ContentVisible,
    string? ContentVisibilityReason,
    AdminMemorySourcePolicyRecord SourcePolicy);

public sealed record AdminMemorySourcePolicyRecord(
    string RetentionClass,
    string Sensitivity,
    string TrustLevel,
    string RedactionStatus,
    bool SourcePayloadIncluded);
