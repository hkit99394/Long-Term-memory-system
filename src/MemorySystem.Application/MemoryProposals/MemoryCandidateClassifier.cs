using MemorySystem.Domain.MemoryTypes;

namespace MemorySystem.Application.MemoryProposals;

internal static class MemoryCandidateClassifier
{
    public static string Classify(MemoryProposalCommand proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (MemoryProposalDecisionRules.IsSessionOnly(proposal))
        {
            return MemoryCandidateClassifications.SessionOnlyInstruction;
        }

        return proposal.MemoryType switch
        {
            MemoryType.Preference when proposal.ScopeType == "user" => MemoryCandidateClassifications.Preference,
            MemoryType.Decision when proposal.ScopeType == "project" => MemoryCandidateClassifications.Decision,
            var value when MemoryType.CanonicalProjectFactLike.Contains(value)
                && proposal.ScopeType == "project" => MemoryCandidateClassifications.ProjectFact,
            MemoryType.RoleLens when proposal.ScopeType is "global" or "org" or "project" => MemoryCandidateClassifications.RoleLens,
            MemoryType.RolePrinciple when proposal.ScopeType is "global" or "org" => MemoryCandidateClassifications.RoleLens,
            MemoryType.ProjectRoleLens when proposal.ScopeType == "project" => MemoryCandidateClassifications.RoleLens,
            MemoryType.AgentPrivate when proposal.ScopeType == "agent" => MemoryCandidateClassifications.AgentPrivate,
            _ => MemoryCandidateClassifications.Unsupported
        };
    }

    public static bool IsSupportedDurable(string candidateKind)
    {
        return candidateKind is MemoryCandidateClassifications.Preference
            or MemoryCandidateClassifications.ProjectFact
            or MemoryCandidateClassifications.Decision
            or MemoryCandidateClassifications.RoleLens
            or MemoryCandidateClassifications.AgentPrivate;
    }
}
