namespace MemorySystem.Application.MemoryReviews;

public interface IMemoryReviewQueue
{
    Task<IReadOnlyList<MemoryReviewRecord>> ListPendingAsync(
        MemoryPendingReviewQuery query,
        CancellationToken cancellationToken = default);
}
