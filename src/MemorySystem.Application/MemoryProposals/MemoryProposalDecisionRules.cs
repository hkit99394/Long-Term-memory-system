namespace MemorySystem.Application.MemoryProposals;

internal static class MemoryProposalDecisionRules
{
    public static bool IsSessionOnly(MemoryProposalCommand proposal)
    {
        return string.Equals(proposal.MemoryType, "session_instruction", StringComparison.Ordinal)
            || string.Equals(proposal.ScopeType, "session", StringComparison.Ordinal)
            || proposal.Namespace.StartsWith("/session/", StringComparison.Ordinal);
    }
}
