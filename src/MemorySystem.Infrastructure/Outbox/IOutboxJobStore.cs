namespace MemorySystem.Infrastructure.Outbox;

public interface IOutboxJobStore
{
    Task<IReadOnlyList<OutboxJob>> LeaseAvailableAsync(
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> CompleteAsync(OutboxJob job, CancellationToken cancellationToken = default);

    Task<bool> MarkFailedAsync(
        OutboxJob job,
        string error,
        bool deadLetter,
        DateTimeOffset? availableAt,
        CancellationToken cancellationToken = default);
}
