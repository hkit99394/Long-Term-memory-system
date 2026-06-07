using MemorySystem.Application.MemoryProposals;

namespace MemorySystem.UnitTests;

public sealed class MinimalMemoryProposalBrokerTests
{
    private static readonly Guid SourceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");

    [Fact]
    public void Decide_accepts_high_confidence_durable_proposal()
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal());

        Assert.Equal(MemoryProposalDecisions.Stored, decision.Decision);
        Assert.Null(decision.MemoryId);
        Assert.Equal(SourceEventId, decision.SourceEventId);
        Assert.Equal(MemoryCandidateClassifications.Preference, decision.CandidateKind);
        Assert.Equal(0.900m, decision.Confidence);
    }

    [Fact]
    public void Decide_rejects_proposal_without_source_event()
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(includeSourceEvent: false));

        Assert.Equal(MemoryProposalDecisions.Rejected, decision.Decision);
        Assert.Contains("sourceEventId", decision.Reason, StringComparison.Ordinal);
        Assert.Equal(MemoryCandidateClassifications.Preference, decision.CandidateKind);
    }

    [Fact]
    public void Decide_sends_low_confidence_proposal_to_review()
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(confidence: 0.45m));

        Assert.Equal(MemoryProposalDecisions.ReviewRequired, decision.Decision);
        Assert.Equal(MemoryCandidateClassifications.Preference, decision.CandidateKind);
        Assert.Equal(0.450m, decision.Confidence);
    }

    [Fact]
    public void Decide_assigns_default_confidence_when_request_confidence_is_missing()
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(confidence: null));

        Assert.Equal(MemoryProposalDecisions.Stored, decision.Decision);
        Assert.Equal(0.850m, decision.Confidence);
    }

    [Fact]
    public void Decide_accepts_structured_role_lens_proposal()
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(
            memoryType: "role_lens",
            scopeType: "org",
            scopeId: "44444444-4444-4444-8444-444444444444",
            namespaceValue: "/org/44444444-4444-4444-8444-444444444444/role/cto/lens",
            roleId: "cto",
            baseMemoryFactId: Guid.Parse("55555555-5555-4555-8555-555555555555")));

        Assert.Equal(MemoryProposalDecisions.Stored, decision.Decision);
        Assert.Equal(MemoryCandidateClassifications.RoleLens, decision.CandidateKind);
        Assert.Equal(0.900m, decision.Confidence);
    }

    [Fact]
    public void Decide_sends_low_trust_evidence_to_review_even_with_high_request_confidence()
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(
            confidence: 0.95m,
            trustLevel: "web_content"));

        Assert.Equal(MemoryProposalDecisions.ReviewRequired, decision.Decision);
        Assert.Equal(0.650m, decision.Confidence);
        Assert.Contains("confidence score", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Decide_keeps_session_scoped_proposal_session_only()
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(
            memoryType: "session_instruction",
            scopeType: "session",
            scopeId: "session-1",
            namespaceValue: "/session/session-1/instructions"));

        Assert.Equal(MemoryProposalDecisions.SessionOnly, decision.Decision);
        Assert.Equal(MemoryCandidateClassifications.SessionOnlyInstruction, decision.CandidateKind);
    }

    [Theory]
    [InlineData("For this answer, keep it short.")]
    [InlineData("Use this temporary file for the current task.")]
    [InlineData("Focus on this one bug today.")]
    public void Decide_keeps_one_off_task_instructions_session_only(string objectValue)
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(
            memoryType: "preference",
            scopeType: "user",
            scopeId: "11111111-1111-4111-8111-111111111111",
            namespaceValue: "/user/11111111-1111-4111-8111-111111111111/preferences",
            subject: "task instruction",
            predicate: "says",
            objectValue: objectValue));

        Assert.Equal(MemoryProposalDecisions.SessionOnly, decision.Decision);
        Assert.Equal(MemoryCandidateClassifications.SessionOnlyInstruction, decision.CandidateKind);
        Assert.Contains("one-off", decision.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("goal", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/goals", MemoryCandidateClassifications.ProjectFact, MemoryProposalDecisions.Stored)]
    [InlineData("target", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/targets", MemoryCandidateClassifications.ProjectFact, MemoryProposalDecisions.Stored)]
    [InlineData("preference", "user", "11111111-1111-4111-8111-111111111111", "/user/11111111-1111-4111-8111-111111111111/preferences", MemoryCandidateClassifications.Preference, MemoryProposalDecisions.Stored)]
    [InlineData("fact", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/facts", MemoryCandidateClassifications.ProjectFact, MemoryProposalDecisions.Stored)]
    [InlineData("decision", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/decisions", MemoryCandidateClassifications.Decision, MemoryProposalDecisions.Stored)]
    [InlineData("rationale", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/rationale", MemoryCandidateClassifications.ProjectFact, MemoryProposalDecisions.Stored)]
    [InlineData("risk", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/risks", MemoryCandidateClassifications.ProjectFact, MemoryProposalDecisions.Stored)]
    [InlineData("assumption", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/assumptions", MemoryCandidateClassifications.ProjectFact, MemoryProposalDecisions.Stored)]
    [InlineData("constraint", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/constraints", MemoryCandidateClassifications.ProjectFact, MemoryProposalDecisions.Stored)]
    [InlineData("requirement", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/requirements", MemoryCandidateClassifications.ProjectFact, MemoryProposalDecisions.Stored)]
    [InlineData("release_evidence", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/release-evidence", MemoryCandidateClassifications.ProjectFact, MemoryProposalDecisions.Stored)]
    [InlineData("role_lens", "org", "44444444-4444-4444-8444-444444444444", "/org/44444444-4444-4444-8444-444444444444/role/cto/lens", MemoryCandidateClassifications.RoleLens, MemoryProposalDecisions.ReviewRequired)]
    [InlineData("project_role_lens", "project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/role/cto/lens", MemoryCandidateClassifications.RoleLens, MemoryProposalDecisions.ReviewRequired)]
    [InlineData("agent_private", "agent", "22222222-2222-4222-8222-222222222222", "/agent/22222222-2222-4222-8222-222222222222/private", MemoryCandidateClassifications.AgentPrivate, MemoryProposalDecisions.Stored)]
    [InlineData("session_instruction", "session", "session-1", "/session/session-1/instructions", MemoryCandidateClassifications.SessionOnlyInstruction, MemoryProposalDecisions.SessionOnly)]
    public void Decide_classifies_supported_candidate_families(
        string memoryType,
        string scopeType,
        string scopeId,
        string namespaceValue,
        string expectedCandidateKind,
        string expectedDecision)
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(
            memoryType: memoryType,
            scopeType: scopeType,
            scopeId: scopeId,
            namespaceValue: namespaceValue));

        Assert.Equal(expectedDecision, decision.Decision);
        Assert.Equal(expectedCandidateKind, decision.CandidateKind);
    }

    [Fact]
    public void Decide_rejects_candidate_that_does_not_match_supported_classification()
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(
            memoryType: "fact",
            scopeType: "user",
            scopeId: "11111111-1111-4111-8111-111111111111",
            namespaceValue: "/user/11111111-1111-4111-8111-111111111111/facts"));

        Assert.Equal(MemoryProposalDecisions.Rejected, decision.Decision);
        Assert.Equal(MemoryCandidateClassifications.Unsupported, decision.CandidateKind);
        Assert.Contains("classification", decision.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("decision", "global", "global", "/global/decisions")]
    [InlineData("decision", "user", "11111111-1111-4111-8111-111111111111", "/user/11111111-1111-4111-8111-111111111111/preferences")]
    [InlineData("goal", "user", "11111111-1111-4111-8111-111111111111", "/user/11111111-1111-4111-8111-111111111111/goals")]
    [InlineData("role_principle", "role", "cto", "/role/cto/shared")]
    [InlineData("role_lens", "role", "cto", "/role/cto/shared")]
    [InlineData("role_principle", "user", "11111111-1111-4111-8111-111111111111", "/user/11111111-1111-4111-8111-111111111111/preferences")]
    [InlineData("project_role_lens", "org", "44444444-4444-4444-8444-444444444444", "/org/44444444-4444-4444-8444-444444444444/role/cto/lens")]
    public void Decide_rejects_candidate_types_outside_supported_scopes(
        string memoryType,
        string scopeType,
        string scopeId,
        string namespaceValue)
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(
            memoryType: memoryType,
            scopeType: scopeType,
            scopeId: scopeId,
            namespaceValue: namespaceValue));

        Assert.Equal(MemoryProposalDecisions.Rejected, decision.Decision);
        Assert.Equal(MemoryCandidateClassifications.Unsupported, decision.CandidateKind);
        Assert.Contains("classification", decision.Reason, StringComparison.Ordinal);
    }

    private static MemoryProposalCommand CreateProposal(
        bool includeSourceEvent = true,
        string memoryType = "preference",
        string scopeType = "user",
        string scopeId = "11111111-1111-4111-8111-111111111111",
        string namespaceValue = "/user/11111111-1111-4111-8111-111111111111/preferences",
        string subject = "technical planning format",
        string predicate = "prefers",
        string objectValue = "concise decision logs",
        decimal? confidence = 0.95m,
        string trustLevel = "user_scoped",
        string? roleId = null,
        Guid? baseMemoryFactId = null)
    {
        return new MemoryProposalCommand(
            includeSourceEvent ? SourceEventId : null,
            SourceEventExists: true,
            memoryType,
            scopeType,
            scopeId,
            namespaceValue,
            "private",
            subject,
            predicate,
            objectValue,
            confidence,
            trustLevel,
            "none",
            roleId,
            baseMemoryFactId);
    }
}
