using MemorySystem.Api.Idempotency;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Events;
using MemorySystem.Infrastructure.MemoryProposals;

namespace MemorySystem.Api.MemoryProposals;

internal sealed class MemoryProposalWorkflow(
    IMemoryProposalBroker broker,
    IMemoryProposalWriteStore writeStore,
    ISourceEventReferenceStore sourceEvents) : IMemoryProposalWorkflow
{
    public async Task<ApiIdempotencyResponse> DecideAsync(
        Guid principalId,
        MemoryProposalRequest request,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken = default)
    {
        if (!MemoryProposalRequestMapper.TryMap(principalId, request, sourceEventExists: false, out var proposal, out var problem))
        {
            return new ApiIdempotencyResponse(problem!.Status ?? StatusCodes.Status400BadRequest, problem);
        }

        var sourceEventExists = request.SourceEventId.HasValue
            && await sourceEvents.ExistsForPrincipalScopeAsync(
                request.SourceEventId.Value,
                principalId,
                proposal.ScopeType,
                proposal.ScopeId,
                cancellationToken);

        proposal = proposal with { SourceEventExists = sourceEventExists };

        var decision = broker.Decide(proposal);

        if (decision.Decision != MemoryProposalDecisions.Stored)
        {
            return new ApiIdempotencyResponse(StatusCodes.Status200OK, decision);
        }

        decision = await writeStore.StoreAsync(
            principalId,
            proposal,
            idempotency.RecordId,
            idempotency.RequestHash,
            cancellationToken);

        return new ApiIdempotencyResponse(
            StatusCodes.Status200OK,
            decision,
            "memory_fact",
            decision.MemoryId,
            IdempotencyAlreadyCompleted: true);
    }
}
