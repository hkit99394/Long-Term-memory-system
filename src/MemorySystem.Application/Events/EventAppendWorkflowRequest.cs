using System.Text.Json;

namespace MemorySystem.Application.Events;

public sealed record EventAppendWorkflowRequest(
    Guid AuthenticatedPrincipalId,
    Guid IdempotencyRecordId,
    string RequestHash,
    Guid? PrincipalId,
    Guid? ConversationId,
    Guid? AgentPrincipalId,
    string? RoleId,
    string? EventType,
    string? ScopeType,
    string? ScopeId,
    Guid? ScopeOrgId,
    string? TrustLevel,
    string? RetentionClass,
    string? Sensitivity,
    string? ExternalPayloadUri,
    JsonElement Payload);
