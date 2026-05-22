using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.Access;

public sealed record MemoryAccessRequest(
    Guid PrincipalId,
    string Permission,
    MemoryScopeResolution Scope,
    string? Namespace = null);
