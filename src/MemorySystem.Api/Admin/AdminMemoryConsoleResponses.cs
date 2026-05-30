namespace MemorySystem.Api.Admin;

public sealed record AdminMemoryFactsResponse(
    IReadOnlyList<AdminMemoryFactResponse> Facts);

public sealed record AdminMemoryFactResponse(
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
    string SourceLink,
    Guid? ProposedByPrincipalId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    AdminMemoryFactPolicyResponse Policy);

public sealed record AdminMemoryFactPolicyResponse(
    bool ContentVisible,
    string? ContentVisibilityReason,
    string SourceRetentionClass,
    string SourceSensitivity,
    string SourceTrustLevel,
    string SourceRedactionStatus,
    bool SourcePayloadIncluded);
