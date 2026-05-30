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

public sealed record AdminSourceEventsResponse(
    IReadOnlyList<AdminSourceEventResponse> Events);

public sealed record AdminSourceEventResponse(
    Guid Id,
    Guid? PrincipalId,
    Guid? ConversationId,
    Guid? AgentPrincipalId,
    string? RoleId,
    string EventType,
    string SourceLink,
    string? ContentHash,
    string? ExternalPayloadUri,
    string RetentionClass,
    string Sensitivity,
    string RedactionStatus,
    DateTimeOffset? RedactedAt,
    Guid? RedactionEventId,
    string? RedactionEventLink,
    string TrustLevel,
    DateTimeOffset CreatedAt,
    AdminSourceEventScopeResponse Scope,
    AdminSourceEventPolicyResponse Policy,
    IReadOnlyList<AdminSourceEventReferenceResponse> References);

public sealed record AdminSourceEventScopeResponse(
    string ScopeType,
    string ScopeId,
    Guid? OrgId,
    Guid? ProjectId,
    Guid? PrincipalId,
    string? RoleId);

public sealed record AdminSourceEventPolicyResponse(
    bool SourcePayloadIncluded,
    string ContentVisibilityReason);

public sealed record AdminSourceEventReferenceResponse(
    string ReferenceType,
    Guid Id,
    string Status,
    string? TargetType,
    Guid? TargetId,
    string? Label);
