using MemorySystem.Domain.Trust;

namespace MemorySystem.Application.MemoryProposals;

internal static class MemoryProposalConfidenceScorer
{
    public const decimal ReviewThreshold = 0.700m;

    public static MemoryProposalConfidenceScore Score(MemoryProposalCommand proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var profile = TrustProfile.For(proposal.TrustLevel);
        var value = proposal.Confidence.HasValue
            ? Math.Min(proposal.Confidence.Value, profile.Maximum)
            : profile.Default;

        return new MemoryProposalConfidenceScore(Round(value), ReviewThreshold);
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private sealed record TrustProfile(decimal Default, decimal Maximum)
    {
        public static TrustProfile For(string trustLevel)
        {
            return trustLevel switch
            {
                MemoryTrustLevel.HumanApproved => new TrustProfile(0.950m, 1.000m),
                MemoryTrustLevel.SystemTrusted => new TrustProfile(0.950m, 0.980m),
                MemoryTrustLevel.UserScoped => new TrustProfile(0.850m, 0.900m),
                MemoryTrustLevel.AgentPrivate => new TrustProfile(0.800m, 0.850m),
                MemoryTrustLevel.ToolOutput => new TrustProfile(0.720m, 0.800m),
                MemoryTrustLevel.WebContent => new TrustProfile(0.550m, 0.650m),
                MemoryTrustLevel.RetrievedUntrusted => new TrustProfile(0.500m, 0.600m),
                _ => new TrustProfile(0.500m, 0.600m)
            };
        }
    }
}
