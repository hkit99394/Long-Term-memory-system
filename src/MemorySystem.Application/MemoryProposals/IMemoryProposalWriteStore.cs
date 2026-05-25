namespace MemorySystem.Application.MemoryProposals;

public interface IMemoryProposalWriteStore
{
    // Implementations must complete the idempotency record transactionally with the durable proposal write.
    Task<MemoryProposalDecision> StoreAsync(
        Guid proposedByPrincipalId,
        MemoryProposalCommand proposal,
        Guid idempotencyRecordId,
        string requestHash,
        CancellationToken cancellationToken = default);
}
