using MemorySystem.Application.Access;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryReviews;

namespace MemorySystem.UnitTests;

public sealed class MemoryReviewQueueTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly string AllowedNamespace = $"/project/{ProjectId}/decisions";

    [Fact]
    public async Task ListPendingAsync_requests_principal_scoped_pending_reviews()
    {
        var accessible = CreateReview(AllowedNamespace, 0);
        var repository = new FakeMemoryReviewRepository(accessible);
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(AllowedNamespace);
        var queue = new MemoryReviewQueue(repository, accessAuthorizer);

        var results = await queue.ListPendingAsync(new MemoryPendingReviewQuery(PrincipalId, Limit: 1));

        var review = Assert.Single(results);
        Assert.Equal(accessible.Id, review.Id);
        Assert.Equal(1, repository.Query?.Limit);
        Assert.Equal(0, repository.Query?.Offset);
        Assert.Equal(PrincipalId, repository.Query?.PrincipalId);
        Assert.Equal(1, accessAuthorizer.CallCount);
    }

    private static MemoryReviewRecord CreateReview(string memoryNamespace, int index)
    {
        var memoryFactId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddSeconds(index);
        var memoryFact = new MemoryFactRecord(
            memoryFactId,
            "project",
            ProjectId.ToString(),
            memoryNamespace,
            UserPrincipalId: null,
            ProjectId,
            OrgId,
            RoleId: null,
            AgentPrincipalId: null,
            "decision",
            "project_shared",
            $"subject {index}",
            "records",
            $"object {index}",
            0.650m,
            "human_approved",
            MemoryFactStatuses.Tentative,
            sourceEventId,
            PrincipalId);

        return new MemoryReviewRecord(
            Guid.NewGuid(),
            memoryFactId,
            MemoryReviewStatuses.Pending,
            ReviewerId: null,
            Notes: null,
            sourceEventId,
            createdAt,
            createdAt,
            memoryFact);
    }

    private sealed class FakeMemoryReviewRepository(MemoryReviewRecord record) : IMemoryReviewRepository
    {
        public MemoryReviewRepositoryQuery? Query { get; private set; }

        public Task<IReadOnlyList<MemoryReviewRecord>> FindPendingAsync(
            MemoryReviewRepositoryQuery query,
            CancellationToken cancellationToken = default)
        {
            Query = query;

            return Task.FromResult<IReadOnlyList<MemoryReviewRecord>>([record]);
        }
    }

    private sealed class FakeMemoryAccessAuthorizer(string allowedNamespace) : IMemoryAccessAuthorizer
    {
        public int CallCount { get; private set; }

        public Task<MemoryAccessDecision> AuthorizeAsync(
            MemoryAccessRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            Assert.Equal(PrincipalId, request.PrincipalId);
            Assert.Equal(MemoryAccessPermissions.Review, request.Permission);

            return Task.FromResult(request.Namespace == allowedNamespace
                ? MemoryAccessDecision.Allow()
                : MemoryAccessDecision.Deny("Principal does not have review access."));
        }
    }
}
