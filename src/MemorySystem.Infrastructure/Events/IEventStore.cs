namespace MemorySystem.Infrastructure.Events;

public interface IEventStore
{
    Task<AppendEventResult> AppendAsync(
        AppendEventCommand command,
        CancellationToken cancellationToken = default);
}
