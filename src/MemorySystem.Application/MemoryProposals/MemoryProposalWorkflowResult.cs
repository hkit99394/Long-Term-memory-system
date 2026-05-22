namespace MemorySystem.Application.MemoryProposals;

public sealed record MemoryProposalWorkflowResult(
    bool IsValid,
    string? InvalidReason,
    MemoryProposalDecision? Decision,
    string? ResourceType = null,
    Guid? ResourceId = null,
    bool IdempotencyAlreadyCompleted = false)
{
    public static MemoryProposalWorkflowResult Invalid(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new MemoryProposalWorkflowResult(false, reason, Decision: null);
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
            "memory_fact",
            decision.MemoryId,
            IdempotencyAlreadyCompleted: true);
    }
}
