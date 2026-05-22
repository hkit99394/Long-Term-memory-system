namespace MemorySystem.Application.MemoryProposals;

public interface IMemoryProposalWriteStore
{
    Task<MemoryProposalDecision> StoreAsync(
        Guid proposedByPrincipalId,
        MemoryProposalCommand proposal,
        Guid idempotencyRecordId,
        string requestHash,
        CancellationToken cancellationToken = default);
}
