namespace MemorySystem.Application.Events;

public sealed record AppendEventCommand(
    Guid PrincipalId,
    Guid? ConversationId,
    Guid? AgentPrincipalId,
    string? RoleId,
    string EventType,
    string ContentJson,
    string ContentHash,
    string? ExternalPayloadUri,
    string RetentionClass,
    string Sensitivity,
    string TrustLevel,
    EventScope Scope);
