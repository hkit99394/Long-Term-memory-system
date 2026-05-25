namespace MemorySystem.Application.MemoryReviews;

public interface IMemoryReviewActionStore
{
    Task<MemoryReviewRecord?> FindPendingAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default);

    Task<MemoryReviewActionStoreResult> ApplyAsync(
        MemoryReviewActionStoreCommand command,
        CancellationToken cancellationToken = default);
}
