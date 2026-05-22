using MemorySystem.Application.Access;

namespace MemorySystem.Application.MemoryFacts;

public sealed class MemoryFactReadService(
    IMemoryFactReadStore readStore,
    IMemoryAccessAuthorizer accessAuthorizer) : IMemoryFactReadService
{
    public async Task<MemoryFactReadResult> ReadAsync(
        Guid principalId,
        Guid memoryFactId,
        CancellationToken cancellationToken = default)
    {
        var memoryFact = await readStore.FindAsync(memoryFactId, cancellationToken);

        if (memoryFact is null)
        {
            return MemoryFactReadResult.NotFound();
        }

        var accessDecision = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                principalId,
                MemoryAccessPermissions.Read,
                memoryFact.ToScopeResolution(),
                memoryFact.Namespace),
            cancellationToken);

        return accessDecision.Allowed
            ? MemoryFactReadResult.FoundMemoryFact(memoryFact)
            : MemoryFactReadResult.NotFound();
    }
}
