namespace MemorySystem.Application.Events;

public interface IEventReadStore
{
    Task<EventRecord?> FindAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);
}
