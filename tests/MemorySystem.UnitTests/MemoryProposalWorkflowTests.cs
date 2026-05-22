using MemorySystem.Application.MemoryProposals;
using MemorySystem.Application.Scopes;

namespace MemorySystem.UnitTests;

public sealed class MemoryProposalWorkflowTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SourceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid IdempotencyRecordId = Guid.Parse("77777777-7777-4777-8777-777777777777");

    [Fact]
    public async Task DecideAsync_returns_invalid_result_without_http_problem_details()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(memoryType: "unsupported"));

        Assert.False(result.IsValid);
        Assert.Equal("memoryType is required and must be supported.", result.InvalidReason);
        Assert.Null(result.Decision);
        Assert.Equal(0, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_rejects_missing_source_event_without_writing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: false);
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest());

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Rejected, result.Decision?.Decision);
        Assert.Equal(SourceEventId, result.Decision?.SourceEventId);
        Assert.Null(result.ResourceType);
        Assert.False(result.IdempotencyAlreadyCompleted);
        Assert.Equal(1, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_returns_review_decision_without_writing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(confidence: 0.45m));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.ReviewRequired, result.Decision?.Decision);
        Assert.Equal(1, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_stores_accepted_proposal_and_returns_resource_metadata()
    {
        var memoryId = Guid.Parse("88888888-8888-4888-8888-888888888888");
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore(memoryId);
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: " PREFERENCE ",
            scopeType: " USER ",
            visibility: null,
            trustLevel: null,
            sensitivity: null));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Stored, result.Decision?.Decision);
        Assert.Equal(memoryId, result.Decision?.MemoryId);
        Assert.Equal("memory_fact", result.ResourceType);
        Assert.Equal(memoryId, result.ResourceId);
        Assert.True(result.IdempotencyAlreadyCompleted);

        Assert.Equal(1, writeStore.CallCount);
        Assert.Equal(PrincipalId, writeStore.ProposedByPrincipalId);
        Assert.Equal(IdempotencyRecordId, writeStore.IdempotencyRecordId);
        Assert.Equal("request-hash", writeStore.RequestHash);
        Assert.NotNull(writeStore.Proposal);
        Assert.True(writeStore.Proposal.SourceEventExists);
        Assert.Equal("preference", writeStore.Proposal.MemoryType);
        Assert.Equal("user", writeStore.Proposal.ScopeType);
        Assert.Equal(PrincipalId.ToString(), writeStore.Proposal.ScopeId);
        Assert.Equal("private", writeStore.Proposal.Visibility);
        Assert.Equal("user_scoped", writeStore.Proposal.TrustLevel);
        Assert.Equal("none", writeStore.Proposal.Sensitivity);
    }

    private static MemoryProposalWorkflow CreateWorkflow(
        ISourceEventReferenceStore sourceEvents,
        IMemoryProposalWriteStore writeStore)
    {
        return new MemoryProposalWorkflow(
            new MinimalMemoryProposalBroker(),
            writeStore,
            sourceEvents,
            new MemoryScopeResolver(new FakeMemoryScopeReferenceStore()));
    }

    private static MemoryProposalWorkflowRequest CreateRequest(
        string? memoryType = "preference",
        string? scopeType = "user",
        string? visibility = "private",
        decimal? confidence = 0.95m,
        string? trustLevel = "user_scoped",
        string? sensitivity = "none")
    {
        return new MemoryProposalWorkflowRequest(
            PrincipalId,
            IdempotencyRecordId,
            "request-hash",
            SourceEventId,
            memoryType,
            scopeType,
            PrincipalId.ToString(),
            $"/user/{PrincipalId}/preferences",
            visibility,
            "technical planning format",
            "prefers",
            "concise decision logs",
            confidence,
            trustLevel,
            sensitivity);
    }

    private sealed class FakeSourceEventReferenceStore(bool sourceEventExists) : ISourceEventReferenceStore
    {
        public int CallCount { get; private set; }

        public Task<bool> ExistsForPrincipalScopeAsync(
            Guid eventId,
            Guid principalId,
            string scopeType,
            string scopeId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            Assert.Equal(SourceEventId, eventId);
            Assert.Equal(PrincipalId, principalId);
            Assert.Equal("user", scopeType);
            Assert.Equal(PrincipalId.ToString(), scopeId);

            return Task.FromResult(sourceEventExists);
        }
    }

    private sealed class FakeMemoryProposalWriteStore(Guid? memoryId = null) : IMemoryProposalWriteStore
    {
        public int CallCount { get; private set; }
        public Guid ProposedByPrincipalId { get; private set; }
        public MemoryProposalCommand Proposal { get; private set; } = null!;
        public Guid IdempotencyRecordId { get; private set; }
        public string RequestHash { get; private set; } = string.Empty;

        public Task<MemoryProposalDecision> StoreAsync(
            Guid proposedByPrincipalId,
            MemoryProposalCommand proposal,
            Guid idempotencyRecordId,
            string requestHash,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposedByPrincipalId = proposedByPrincipalId;
            Proposal = proposal;
            IdempotencyRecordId = idempotencyRecordId;
            RequestHash = requestHash;

            return Task.FromResult(new MemoryProposalDecision(
                MemoryProposalDecisions.Stored,
                "The proposal was stored as durable memory.",
                memoryId ?? Guid.NewGuid(),
                proposal.SourceEventId));
        }
    }

    private sealed class FakeMemoryScopeReferenceStore : IMemoryScopeReferenceStore
    {
        public Task<bool> OrganizationExistsAsync(Guid orgId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<ProjectScopeReference?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProjectScopeReference?>(new ProjectScopeReference(projectId, Guid.NewGuid()));
        }

        public Task<bool> PrincipalExistsAsync(
            Guid principalId,
            string? principalType = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
