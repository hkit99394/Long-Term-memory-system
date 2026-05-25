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
                "human_approved" => new TrustProfile(0.950m, 1.000m),
                "system_trusted" => new TrustProfile(0.950m, 0.980m),
                "user_scoped" => new TrustProfile(0.850m, 0.900m),
                "agent_private" => new TrustProfile(0.800m, 0.850m),
                "tool_output" => new TrustProfile(0.720m, 0.800m),
                "web_content" => new TrustProfile(0.550m, 0.650m),
                "retrieved_untrusted" => new TrustProfile(0.500m, 0.600m),
                _ => new TrustProfile(0.500m, 0.600m)
            };
        }
    }
}
