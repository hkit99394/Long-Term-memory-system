namespace MemorySystem.Infrastructure.Workers;

public interface IWorkerHeartbeatStore
{
    Task RecordAsync(
        WorkerHeartbeatUpdate update,
        CancellationToken cancellationToken = default);

    Task<WorkerHeartbeatSnapshot?> ReadLatestAsync(
        string workerType,
        CancellationToken cancellationToken = default);
}
