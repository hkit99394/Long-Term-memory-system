namespace MemorySystem.Application.MemoryProposals;

public sealed class MinimalMemoryProposalBroker : IMemoryProposalBroker
{
    private const decimal ReviewConfidenceThreshold = 0.700m;

    private static readonly HashSet<string> DurableMemoryTypes = new(StringComparer.Ordinal)
    {
        "preference",
        "decision",
        "fact",
        "role_principle",
        "project_role_lens",
        "agent_private"
    };

    public MemoryProposalDecision Decide(MemoryProposalCommand proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (!proposal.SourceEventId.HasValue)
        {
            return Reject(proposal, "A sourceEventId is required before the broker can accept durable memory.");
        }

        if (!proposal.SourceEventExists)
        {
            return Reject(proposal, "The source event does not exist.");
        }

        if (MemoryProposalDecisionRules.IsSessionOnly(proposal))
        {
            return new MemoryProposalDecision(
                MemoryProposalDecisions.SessionOnly,
                "The proposal is scoped to the current session and should not become durable memory.",
                MemoryId: null,
                proposal.SourceEventId);
        }

        if (!DurableMemoryTypes.Contains(proposal.MemoryType))
        {
            return Reject(proposal, "The memory type is not supported by the M2 broker.");
        }

        if (string.IsNullOrWhiteSpace(proposal.Subject)
            || string.IsNullOrWhiteSpace(proposal.Predicate)
            || string.IsNullOrWhiteSpace(proposal.Object))
        {
            return Reject(proposal, "Durable memory proposals require subject, predicate, and object.");
        }

        if (IsUntrustedPolicyWrite(proposal))
        {
            return Reject(proposal, "Untrusted retrieved or web content cannot write policy-level durable memory.");
        }

        if (RequiresReview(proposal))
        {
            return new MemoryProposalDecision(
                MemoryProposalDecisions.ReviewRequired,
                "The proposal is plausible but needs human review before durable storage.",
                MemoryId: null,
                proposal.SourceEventId);
        }

        return new MemoryProposalDecision(
            MemoryProposalDecisions.Stored,
            "The proposal is accepted for durable storage.",
            MemoryId: null,
            proposal.SourceEventId);
    }

    private static MemoryProposalDecision Reject(MemoryProposalCommand proposal, string reason)
    {
        return new MemoryProposalDecision(
            MemoryProposalDecisions.Rejected,
            reason,
            MemoryId: null,
            proposal.SourceEventId);
    }

    private static bool IsUntrustedPolicyWrite(MemoryProposalCommand proposal)
    {
        var untrusted = string.Equals(proposal.TrustLevel, "web_content", StringComparison.Ordinal)
            || string.Equals(proposal.TrustLevel, "retrieved_untrusted", StringComparison.Ordinal);

        return untrusted
            && (string.Equals(proposal.ScopeType, "global", StringComparison.Ordinal)
                || string.Equals(proposal.ScopeType, "org", StringComparison.Ordinal)
                || proposal.Namespace.Contains("/policies", StringComparison.Ordinal));
    }

    private static bool RequiresReview(MemoryProposalCommand proposal)
    {
        return proposal.Confidence is null
            || proposal.Confidence < ReviewConfidenceThreshold
            || string.Equals(proposal.Sensitivity, "secret", StringComparison.Ordinal)
            || string.Equals(proposal.Sensitivity, "regulated", StringComparison.Ordinal);
    }
}
