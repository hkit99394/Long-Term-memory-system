using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.MemoryFacts;

public sealed record MemoryFactSearchQuery(
    MemoryScopeResolution Scope,
    string? MemoryType = null,
    string? Subject = null,
    string Status = MemoryFactStatuses.Active,
    int Limit = 50);
