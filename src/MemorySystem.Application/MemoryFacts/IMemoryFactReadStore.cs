namespace MemorySystem.Application.MemoryFacts;

public interface IMemoryFactReadStore
{
    Task<MemoryFactRecord?> FindAsync(
        Guid memoryFactId,
        CancellationToken cancellationToken = default);
}
