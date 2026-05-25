namespace MemorySystem.Application.MemoryProposals;

public sealed record MemoryProposalWorkflowResult(
    bool IsValid,
    string? InvalidReason,
    MemoryProposalDecision? Decision,
    string? ResourceType = null,
    Guid? ResourceId = null,
    bool IdempotencyAlreadyCompleted = false,
    int FailureStatusCode = 400)
{
    public static MemoryProposalWorkflowResult Invalid(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new MemoryProposalWorkflowResult(false, reason, Decision: null);
    }

    public static MemoryProposalWorkflowResult Forbidden(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new MemoryProposalWorkflowResult(
            false,
            reason,
            Decision: null,
            FailureStatusCode: 403);
    }

    public static MemoryProposalWorkflowResult Decided(MemoryProposalDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new MemoryProposalWorkflowResult(true, InvalidReason: null, decision);
    }

    public static MemoryProposalWorkflowResult Stored(MemoryProposalDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new MemoryProposalWorkflowResult(
            true,
            InvalidReason: null,
            decision,
            ResourceTypeFor(decision),
            decision.MemoryId,
            IdempotencyAlreadyCompleted: true);
    }

    public static MemoryProposalWorkflowResult CompletedDecision(MemoryProposalDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new MemoryProposalWorkflowResult(
            true,
            InvalidReason: null,
            decision,
            IdempotencyAlreadyCompleted: true);
    }

    private static string ResourceTypeFor(MemoryProposalDecision decision)
    {
        return decision.CandidateKind == MemoryCandidateClassifications.RoleLens
            ? "role_memory_lens"
            : "memory_fact";
    }
}
