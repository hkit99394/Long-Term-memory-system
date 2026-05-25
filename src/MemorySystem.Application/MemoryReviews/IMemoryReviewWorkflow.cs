namespace MemorySystem.Application.MemoryReviews;

public interface IMemoryReviewWorkflow
{
    Task<MemoryReviewWorkflowResult> CompleteAsync(
        MemoryReviewActionCommand command,
        CancellationToken cancellationToken = default);
}
