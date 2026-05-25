namespace MemorySystem.Application.MemoryReviews;

public interface IMemoryReviewRepository
{
    Task<IReadOnlyList<MemoryReviewRecord>> FindPendingAsync(
        MemoryReviewRepositoryQuery query,
        CancellationToken cancellationToken = default);
}
