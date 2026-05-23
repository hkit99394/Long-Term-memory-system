namespace MemorySystem.Application.MemoryFacts;

public interface IMemoryFactRepository
{
    Task<MemoryFactRecord?> FindAsync(
        Guid memoryFactId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryFactRecord>> FindByScopeAsync(
        MemoryFactScopeQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryFactRecord>> SearchAsync(
        MemoryFactSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<MemoryFactRecord> StoreAsync(
        MemoryFactWriteCommand command,
        CancellationToken cancellationToken = default);
}
