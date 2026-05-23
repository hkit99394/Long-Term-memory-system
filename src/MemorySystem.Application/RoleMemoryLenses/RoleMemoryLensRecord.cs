using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.RoleMemoryLenses;

public sealed record RoleMemoryLensRecord(
    Guid Id,
    string RoleId,
    string ScopeType,
    string ScopeId,
    Guid? OrgId,
    Guid? ProjectId,
    Guid BaseMemoryFactId,
    string Interpretation,
    decimal Confidence,
    string Status,
    Guid SourceEventId)
{
    public bool IsSharedRolePrinciple => ProjectId is null;

    public bool IsProjectRoleLens => ProjectId is not null;

    public MemoryScopeResolution ToScopeResolution()
    {
        return ScopeType switch
        {
            "global" => new MemoryScopeResolution(ScopeType, ScopeId),
            "org" => new MemoryScopeResolution(ScopeType, ScopeId, OrgId: OrgId),
            "project" => new MemoryScopeResolution(ScopeType, ScopeId, OrgId: OrgId, ProjectId: ProjectId),
            _ => throw new InvalidOperationException($"Unsupported role memory lens scope type '{ScopeType}'.")
        };
    }
}
