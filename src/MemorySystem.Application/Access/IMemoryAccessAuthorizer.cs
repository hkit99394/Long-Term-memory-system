namespace MemorySystem.Application.Access;

public interface IMemoryAccessAuthorizer
{
    Task<MemoryAccessDecision> AuthorizeAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken = default);
}
