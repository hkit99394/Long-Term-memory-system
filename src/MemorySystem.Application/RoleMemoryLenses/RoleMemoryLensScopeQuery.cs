using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.RoleMemoryLenses;

public sealed record RoleMemoryLensScopeQuery(
    MemoryScopeResolution Scope,
    string RoleId,
    string Status = MemoryFactStatuses.Active,
    int Limit = 50);
