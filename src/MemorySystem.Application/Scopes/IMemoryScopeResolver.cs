namespace MemorySystem.Application.Scopes;

public interface IMemoryScopeResolver
{
    Task<MemoryScopeResolveResult> ResolveEventScopeAsync(
        MemoryEventScopeRequest request,
        CancellationToken cancellationToken = default);

    Task<MemoryScopeResolveResult> ResolveProposalScopeAsync(
        MemoryProposalScopeRequest request,
        CancellationToken cancellationToken = default);
}
