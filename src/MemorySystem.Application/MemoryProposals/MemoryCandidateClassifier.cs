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
            "preference" when proposal.ScopeType == "user" => MemoryCandidateClassifications.Preference,
            "fact" when proposal.ScopeType == "project" => MemoryCandidateClassifications.ProjectFact,
            "decision" when proposal.ScopeType == "project" => MemoryCandidateClassifications.Decision,
            "role_principle" when proposal.ScopeType is "global" or "org" => MemoryCandidateClassifications.RoleLens,
            "project_role_lens" when proposal.ScopeType == "project" => MemoryCandidateClassifications.RoleLens,
            "agent_private" when proposal.ScopeType == "agent" => MemoryCandidateClassifications.AgentPrivate,
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
