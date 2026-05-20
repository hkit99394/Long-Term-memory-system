using MemorySystem.Infrastructure.Outbox;

namespace MemorySystem.Worker;

public interface IOutboxJobHandler
{
    bool CanHandle(string jobType);

    // Handlers must be idempotent because expired leases can be retried by another worker.
    Task ProcessAsync(OutboxJob job, CancellationToken cancellationToken);
}
