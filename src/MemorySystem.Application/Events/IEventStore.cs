namespace MemorySystem.Application.Events;

public interface IEventStore
{
    Task<AppendEventResult> AppendAsync(
        AppendEventCommand command,
        Guid idempotencyRecordId,
        string requestHash,
        CancellationToken cancellationToken = default);
}
