using MemorySystem.Api.Idempotency;

namespace MemorySystem.Api.MemoryProposals;

internal interface IMemoryProposalWorkflow
{
    Task<ApiIdempotencyResponse> DecideAsync(
        Guid principalId,
        MemoryProposalRequest request,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken = default);
}
