namespace MemorySystem.Infrastructure.Events;

public interface ISourceEventReferenceStore
{
    Task<bool> ExistsForPrincipalScopeAsync(
        Guid eventId,
        Guid principalId,
        string scopeType,
        string scopeId,
        CancellationToken cancellationToken = default);
}
