namespace MemorySystem.Application.MemoryProposals;

public interface ISourceEventReferenceStore
{
    Task<bool> ExistsForPrincipalScopeAsync(
        Guid eventId,
        Guid principalId,
        string scopeType,
        string scopeId,
        CancellationToken cancellationToken = default);
}
