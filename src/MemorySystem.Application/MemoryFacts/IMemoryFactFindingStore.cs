namespace MemorySystem.Application.MemoryFacts;

public interface IMemoryFactFindingStore
{
    Task<MemoryFactFindingStoreResult> QueryFactsAsync(
        MemoryFactFindingQuery query,
        CancellationToken cancellationToken = default);
}
