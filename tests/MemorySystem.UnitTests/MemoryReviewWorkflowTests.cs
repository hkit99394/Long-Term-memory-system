using MemorySystem.Application.Access;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Application.MemoryReviews;

namespace MemorySystem.UnitTests;

public sealed class MemoryReviewWorkflowTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ReviewId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid MemoryFactId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid SourceEventId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public async Task CompleteAsync_returns_not_found_when_pending_review_is_completed_before_apply()
    {
        var actionStore = new RaceMemoryReviewActionStore(CreatePendingReview());
        var workflow = new MemoryReviewWorkflow(
            actionStore,
            new AllowingSourceEventReferenceStore(),
            new AllowingMemoryAccessAuthorizer());

        var result = await workflow.CompleteAsync(new MemoryReviewActionCommand(
            PrincipalId,
            Guid.NewGuid(),
            "sha256:test",
            ReviewId,
            MemoryReviewActions.Approve,
            SourceEventId,
            Notes: null,
            Subject: null,
            Predicate: null,
            Object: null));

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.FailureStatusCode);
        Assert.Contains("already been completed", result.Error, StringComparison.Ordinal);
        Assert.True(actionStore.ApplyWasCalled);
    }

    private static MemoryReviewRecord CreatePendingReview()
    {
        var now = DateTimeOffset.UtcNow;

        return new MemoryReviewRecord(
            ReviewId,
            MemoryFactId,
            MemoryReviewStatuses.Pending,
            ReviewerId: null,
            Notes: "Needs review.",
            SourceEventId,
            now,
            now,
            new MemoryFactRecord(
                MemoryFactId,
                "global",
                "global",
                "/global/decisions",
                UserPrincipalId: null,
                ProjectId: null,
                OrgId: null,
                RoleId: null,
                AgentPrincipalId: null,
                "decision",
                "system",
                "Memory review race handling",
                "returns",
                "not found when another reviewer completes first",
                0.9m,
                "human_approved",
                MemoryFactStatuses.Tentative,
                SourceEventId,
                PrincipalId));
    }

    private sealed class RaceMemoryReviewActionStore(MemoryReviewRecord review) : IMemoryReviewActionStore
    {
        public bool ApplyWasCalled { get; private set; }

        public Task<MemoryReviewRecord?> FindPendingAsync(
            Guid reviewId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MemoryReviewRecord?>(review);
        }

        public Task<MemoryReviewActionStoreResult> ApplyAsync(
            MemoryReviewActionStoreCommand command,
            CancellationToken cancellationToken = default)
        {
            ApplyWasCalled = true;

            return Task.FromResult(MemoryReviewActionStoreResult.NotApplied());
        }
    }

    private sealed class AllowingSourceEventReferenceStore : ISourceEventReferenceStore
    {
        public Task<SourceEventReference?> FindForPrincipalScopeAsync(
            Guid eventId,
            Guid principalId,
            string scopeType,
            string scopeId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SourceEventReference?>(
                new SourceEventReference(eventId, "human_approved", "none"));
        }
    }

    private sealed class AllowingMemoryAccessAuthorizer : IMemoryAccessAuthorizer
    {
        public Task<MemoryAccessDecision> AuthorizeAsync(
            MemoryAccessRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MemoryAccessDecision.Allow());
        }
    }
}
