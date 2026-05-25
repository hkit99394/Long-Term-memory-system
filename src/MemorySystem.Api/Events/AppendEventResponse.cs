using System.Text.Json;

namespace MemorySystem.Api.Events;

public sealed record AppendEventResponse(Guid Id);

public sealed record EventResponse(
    Guid Id,
    Guid? PrincipalId,
    Guid? ConversationId,
    Guid? AgentPrincipalId,
    string? RoleId,
    string EventType,
    JsonElement Content,
    string? ContentHash,
    string? ExternalPayloadUri,
    string RetentionClass,
    string Sensitivity,
    string TrustLevel,
    DateTimeOffset CreatedAt,
    EventScopeResponse Scope);

public sealed record EventScopeResponse(
    string ScopeType,
    string ScopeId,
    Guid? OrgId,
    Guid? ProjectId,
    Guid? PrincipalId,
    string? RoleId,
    Guid? AgentPrincipalId,
    Guid? ConversationId);
