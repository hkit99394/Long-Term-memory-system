namespace MemorySystem.Application.Events;

public interface IEventReadService
{
    Task<EventReadResult> ReadAsync(
        Guid principalId,
        Guid eventId,
        CancellationToken cancellationToken = default);
}
