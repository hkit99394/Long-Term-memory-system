namespace MemorySystem.Application.MemoryReviews;

public interface IMemoryContextFeedbackObservationStore
{
    Task<IReadOnlyList<MemoryContextFeedbackObservationRecord>> ListAsync(
        MemoryContextFeedbackObservationQuery query,
        CancellationToken cancellationToken = default);

    Task<MemoryContextFeedbackReviewResult> OpenReviewAsync(
        MemoryContextFeedbackReviewCommand command,
        CancellationToken cancellationToken = default);
}
