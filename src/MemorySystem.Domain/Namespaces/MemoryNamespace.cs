using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Scopes;

namespace MemorySystem.Domain.Namespaces;

public sealed record MemoryNamespace(
    string Value,
    MemoryScope Scope,
    IReadOnlyList<string> Segments,
    MemoryRoleId? RoleId = null)
{
    public string ScopeType => Scope.ScopeType;

    public string ScopeId => Scope.ScopeId;
}
