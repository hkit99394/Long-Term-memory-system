using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.Events;

public sealed record EventRecord(
    Guid Id,
    Guid? PrincipalId,
    Guid? ConversationId,
    Guid? AgentPrincipalId,
    string? RoleId,
    string EventType,
    string ContentJson,
    string? ContentHash,
    string? ExternalPayloadUri,
    string RetentionClass,
    string Sensitivity,
    string TrustLevel,
    DateTimeOffset CreatedAt,
    MemoryScopeResolution Scope);
