namespace MemorySystem.Application.Scopes;

public sealed record MemoryEventScopeRequest(
    Guid AuthenticatedPrincipalId,
    string? ScopeType,
    string? ScopeId,
    Guid? ScopeOrgId,
    Guid? ConversationId,
    Guid? AgentPrincipalId,
    string? RoleId);
