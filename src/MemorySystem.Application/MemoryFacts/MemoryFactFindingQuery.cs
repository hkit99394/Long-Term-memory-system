namespace MemorySystem.Application.MemoryFacts;

public sealed record MemoryFactFindingQuery(
    Guid PrincipalId,
    string Query,
    string? TargetScopeType = null,
    string? TargetScopeId = null,
    string? RoleId = null,
    IReadOnlyList<string>? Namespaces = null,
    IReadOnlyList<string>? MemoryTypes = null,
    bool IncludeContradictions = false,
    bool IncludeExcluded = false,
    int Limit = 0);
