namespace MemorySystem.Infrastructure.Events;

public interface ISourceEventReferenceStore
{
    Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default);
}
