using MemorySystem.Application.Access;

namespace MemorySystem.Application.MemoryReviews;

public sealed class MemoryReviewQueue(
    IMemoryReviewRepository repository,
    IMemoryAccessAuthorizer accessAuthorizer) : IMemoryReviewQueue
{
    private const int MaxLimit = 50;

    public async Task<IReadOnlyList<MemoryReviewRecord>> ListPendingAsync(
        MemoryPendingReviewQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, $"Pending review limit must be between 1 and {MaxLimit}.");
        }

        var candidates = await repository.FindPendingAsync(
            new MemoryReviewRepositoryQuery(query.Limit, PrincipalId: query.PrincipalId),
            cancellationToken);
        var results = new List<MemoryReviewRecord>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var accessDecision = await accessAuthorizer.AuthorizeAsync(
                new MemoryAccessRequest(
                    query.PrincipalId,
                    MemoryAccessPermissions.Review,
                    candidate.MemoryFact.ToScopeResolution(),
                    candidate.MemoryFact.Namespace),
                cancellationToken);

            if (!accessDecision.Allowed)
            {
                continue;
            }

            results.Add(candidate);
        }

        return results;
    }
}
