namespace MemorySystem.Application.MemoryFacts;

public interface IMemoryFactReadService
{
    Task<MemoryFactReadResult> ReadAsync(
        Guid principalId,
        Guid memoryFactId,
        CancellationToken cancellationToken = default);
}
