using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.MemoryFacts;

public sealed record MemoryFactScopeQuery(
    MemoryScopeResolution Scope,
    string? MemoryType = null,
    string Status = MemoryFactStatuses.Active,
    int Limit = 50);
