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
    }

    [Fact]
    public void Decide_rejects_proposal_without_source_event()
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(includeSourceEvent: false));

        Assert.Equal(MemoryProposalDecisions.Rejected, decision.Decision);
        Assert.Contains("sourceEventId", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Decide_sends_low_confidence_proposal_to_review()
    {
        var decision = new MinimalMemoryProposalBroker().Decide(CreateProposal(confidence: 0.45m));

        Assert.Equal(MemoryProposalDecisions.ReviewRequired, decision.Decision);
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
    }

    private static MemoryProposalCommand CreateProposal(
        bool includeSourceEvent = true,
        string memoryType = "preference",
        string scopeType = "user",
        string scopeId = "11111111-1111-4111-8111-111111111111",
        string namespaceValue = "/user/11111111-1111-4111-8111-111111111111/preferences",
        decimal confidence = 0.95m)
    {
        return new MemoryProposalCommand(
            includeSourceEvent ? SourceEventId : null,
            SourceEventExists: true,
            memoryType,
            scopeType,
            scopeId,
            namespaceValue,
            "private",
            "technical planning format",
            "prefers",
            "concise decision logs",
            confidence,
            "user_scoped",
            "none");
    }
}
