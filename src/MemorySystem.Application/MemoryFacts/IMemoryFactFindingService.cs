namespace MemorySystem.Application.MemoryFacts;

public interface IMemoryFactFindingService
{
    Task<MemoryFactFindingResult> QueryFactsAsync(
        MemoryFactFindingQuery query,
        CancellationToken cancellationToken = default);
}
