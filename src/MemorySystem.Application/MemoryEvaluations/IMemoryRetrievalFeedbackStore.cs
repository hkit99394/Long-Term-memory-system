namespace MemorySystem.Application.MemoryEvaluations;

public interface IMemoryRetrievalFeedbackStore
{
    Task<MemoryRetrievalFeedbackRecord> StoreAsync(
        MemoryRetrievalFeedbackCommand command,
        CancellationToken cancellationToken = default);
}
