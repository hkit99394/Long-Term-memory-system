namespace MemorySystem.Api.MemoryFacts;

public sealed record MemoryQueryFactsRequest(
    string? Query,
    MemoryQueryFactsTargetScopeRequest? TargetScope = null,
    string? RoleId = null,
    IReadOnlyList<string>? Namespaces = null,
    IReadOnlyList<string>? MemoryTypes = null,
    bool IncludeContradictions = false,
    bool IncludeExcluded = false,
    int? Limit = null);

public sealed record MemoryQueryFactsTargetScopeRequest(
    string? ScopeType,
    string? ScopeId);
