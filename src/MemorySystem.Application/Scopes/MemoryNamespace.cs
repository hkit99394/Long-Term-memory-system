namespace MemorySystem.Application.Scopes;

public sealed record MemoryNamespace(
    string Value,
    string ScopeType,
    string ScopeId,
    IReadOnlyList<string> Segments,
    string? RoleId = null);
