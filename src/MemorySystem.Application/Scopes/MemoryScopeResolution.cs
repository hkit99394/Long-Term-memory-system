namespace MemorySystem.Application.Scopes;

public sealed record MemoryScopeResolution(
    string ScopeType,
    string ScopeId,
    Guid? OrgId = null,
    Guid? ProjectId = null,
    Guid? PrincipalId = null,
    string? RoleId = null,
    string? ScopeRoleId = null,
    Guid? ConversationId = null,
    Guid? AgentPrincipalId = null);
