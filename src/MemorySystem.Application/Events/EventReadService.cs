using MemorySystem.Application.Access;
using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.Events;

public sealed class EventReadService(
    IEventReadStore eventReadStore,
    IMemoryAccessAuthorizer accessAuthorizer) : IEventReadService
{
    public async Task<EventReadResult> ReadAsync(
        Guid principalId,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        if (principalId == Guid.Empty || eventId == Guid.Empty)
        {
            return EventReadResult.NotFound();
        }

        var eventRecord = await eventReadStore.FindAsync(eventId, cancellationToken);

        if (eventRecord is null)
        {
            return EventReadResult.NotFound();
        }

        var accessDecision = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                principalId,
                MemoryAccessPermissions.Read,
                eventRecord.Scope,
                EventAuthorizationNamespace(eventRecord.Scope)),
            cancellationToken);

        return accessDecision.Allowed
            ? EventReadResult.FoundEvent(eventRecord)
            : EventReadResult.NotFound();
    }

    private static string? EventAuthorizationNamespace(MemoryScopeResolution scope)
    {
        return scope.ScopeType is "global" or "session"
            ? MemoryNamespaceParser.BuildScopePrefix(scope.ScopeType, scope.ScopeId) + "events"
            : null;
    }
}
