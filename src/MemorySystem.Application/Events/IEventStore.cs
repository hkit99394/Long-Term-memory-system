namespace MemorySystem.Application.Events;

public interface IEventStore
{
    // Implementations must complete the idempotency record transactionally with the appended event.
    Task<AppendEventResult> AppendAsync(
        AppendEventCommand command,
        Guid idempotencyRecordId,
        string requestHash,
        CancellationToken cancellationToken = default);
}
