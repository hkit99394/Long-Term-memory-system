namespace MemorySystem.Application.MemoryProposals;

internal sealed record MemoryProposalConfidenceScore(decimal Value, decimal ReviewThreshold)
{
    public bool RequiresReview => Value < ReviewThreshold;
}
