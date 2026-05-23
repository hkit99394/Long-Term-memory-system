using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.MemoryFacts;

public sealed record MemoryFactRecord(
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
    string Subject,
    string Predicate,
    string Object,
    decimal Confidence,
    string TrustLevel,
    string Status,
    Guid SourceEventId,
    Guid? ProposedByPrincipalId)
{
    public MemoryScopeResolution ToScopeResolution()
    {
        return ScopeType switch
        {
            "global" => new MemoryScopeResolution(ScopeType, ScopeId),
            "org" => new MemoryScopeResolution(ScopeType, ScopeId, OrgId: OrgId),
            "project" => new MemoryScopeResolution(ScopeType, ScopeId, OrgId: OrgId, ProjectId: ProjectId),
            "user" => new MemoryScopeResolution(ScopeType, ScopeId, PrincipalId: UserPrincipalId),
            "agent" => new MemoryScopeResolution(
                ScopeType,
                ScopeId,
                PrincipalId: AgentPrincipalId,
                AgentPrincipalId: AgentPrincipalId),
            "role" => new MemoryScopeResolution(ScopeType, ScopeId, RoleId: RoleId, ScopeRoleId: RoleId),
            "session" => new MemoryScopeResolution(ScopeType, ScopeId),
            _ => throw new InvalidOperationException($"Unsupported memory fact scope type '{ScopeType}'.")
        };
    }
}
