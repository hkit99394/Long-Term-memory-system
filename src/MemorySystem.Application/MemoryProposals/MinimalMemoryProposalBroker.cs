using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Sensitivity;
using MemorySystem.Domain.Trust;

namespace MemorySystem.Application.MemoryProposals;

public sealed class MinimalMemoryProposalBroker : IMemoryProposalBroker
{
    public MemoryProposalDecision Decide(MemoryProposalCommand proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var candidateKind = proposal.CandidateKind;

        if (!proposal.SourceEventId.HasValue)
        {
            return Reject(proposal, "A sourceEventId is required before the broker can accept durable memory.", candidateKind);
        }

        if (!proposal.SourceEventExists)
        {
            return Reject(proposal, "The source event does not exist.", candidateKind);
        }

        if (candidateKind == MemoryCandidateClassifications.SessionOnlyInstruction)
        {
            return new MemoryProposalDecision(
                MemoryProposalDecisions.SessionOnly,
                BuildSessionOnlyReason(proposal),
                MemoryId: null,
                proposal.SourceEventId,
                candidateKind);
        }

        if (!MemoryCandidateClassifier.IsSupportedDurable(candidateKind))
        {
            return Reject(proposal, "The proposal does not match a supported M5 candidate classification.", candidateKind);
        }

        if (string.IsNullOrWhiteSpace(proposal.Subject)
            || string.IsNullOrWhiteSpace(proposal.Predicate)
            || string.IsNullOrWhiteSpace(proposal.Object))
        {
            return Reject(proposal, "Durable memory proposals require subject, predicate, and object.", candidateKind);
        }

        if (IsUntrustedPolicyWrite(proposal))
        {
            return Reject(proposal, "Untrusted retrieved or web content cannot write policy-level durable memory.", candidateKind);
        }

        var confidenceScore = MemoryProposalConfidenceScorer.Score(proposal);

        if (candidateKind == MemoryCandidateClassifications.RoleLens
            && !HasStructuredRoleLensFields(proposal, out var roleLensReason))
        {
            return new MemoryProposalDecision(
                MemoryProposalDecisions.ReviewRequired,
                roleLensReason,
                MemoryId: null,
                proposal.SourceEventId,
                candidateKind,
                confidenceScore.Value);
        }

        if (RequiresReview(proposal, confidenceScore, out var reviewReason))
        {
            return new MemoryProposalDecision(
                MemoryProposalDecisions.ReviewRequired,
                reviewReason,
                MemoryId: null,
                proposal.SourceEventId,
                candidateKind,
                confidenceScore.Value);
        }

        return new MemoryProposalDecision(
            MemoryProposalDecisions.Stored,
            "The proposal is accepted for durable storage.",
            MemoryId: null,
            proposal.SourceEventId,
            candidateKind,
            confidenceScore.Value);
    }

    private static MemoryProposalDecision Reject(
        MemoryProposalCommand proposal,
        string reason,
        string candidateKind)
    {
        return new MemoryProposalDecision(
            MemoryProposalDecisions.Rejected,
            reason,
            MemoryId: null,
            proposal.SourceEventId,
            candidateKind);
    }

    private static bool IsUntrustedPolicyWrite(MemoryProposalCommand proposal)
    {
        var untrusted = string.Equals(proposal.TrustLevel, MemoryTrustLevel.WebContent, StringComparison.Ordinal)
            || string.Equals(proposal.TrustLevel, MemoryTrustLevel.RetrievedUntrusted, StringComparison.Ordinal);

        return untrusted
            && (string.Equals(proposal.ScopeType, "global", StringComparison.Ordinal)
                || string.Equals(proposal.ScopeType, "org", StringComparison.Ordinal)
                || proposal.Namespace.Contains("/policies", StringComparison.Ordinal));
    }

    private static string BuildSessionOnlyReason(MemoryProposalCommand proposal)
    {
        return MemoryProposalDecisionRules.IsOneOffInstruction(proposal)
            ? "The proposal is a one-off task instruction and should stay session-only."
            : "The proposal is scoped to the current session and should not become durable memory.";
    }

    private static bool RequiresReview(
        MemoryProposalCommand proposal,
        MemoryProposalConfidenceScore confidenceScore,
        out string reason)
    {
        if (string.Equals(proposal.Sensitivity, MemorySensitivity.Secret, StringComparison.Ordinal)
            || string.Equals(proposal.Sensitivity, MemorySensitivity.Regulated, StringComparison.Ordinal))
        {
            reason = "The proposal contains sensitive content and needs human review before durable storage.";
            return true;
        }

        if (confidenceScore.RequiresReview)
        {
            reason = "The proposal confidence score is below the durable storage threshold and needs human review.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool HasStructuredRoleLensFields(MemoryProposalCommand proposal, out string reason)
    {
        if (proposal.ScopeType is not ("global" or "org" or "project"))
        {
            reason = "Role-lens proposals require global, organization, or project scope before durable storage.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(proposal.RoleId)
            || !MemoryRoleId.TryNormalizeIdentifier(proposal.RoleId, out _, out _))
        {
            reason = "Role-lens proposals require a supported roleId before durable storage.";
            return false;
        }

        if (!proposal.BaseMemoryFactId.HasValue || proposal.BaseMemoryFactId.Value == Guid.Empty)
        {
            reason = "Role-lens proposals require baseMemoryFactId before durable storage.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
