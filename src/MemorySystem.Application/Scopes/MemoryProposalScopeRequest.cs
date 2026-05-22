namespace MemorySystem.Application.Scopes;

public sealed record MemoryProposalScopeRequest(
    Guid AuthenticatedPrincipalId,
    string? ScopeType,
    string? ScopeId,
    string? Namespace);
