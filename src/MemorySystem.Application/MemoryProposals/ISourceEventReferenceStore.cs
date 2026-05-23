namespace MemorySystem.Application.MemoryProposals;

public interface ISourceEventReferenceStore
{
    Task<SourceEventReference?> FindForPrincipalScopeAsync(
        Guid eventId,
        Guid principalId,
        string scopeType,
        string scopeId,
        CancellationToken cancellationToken = default);
}
