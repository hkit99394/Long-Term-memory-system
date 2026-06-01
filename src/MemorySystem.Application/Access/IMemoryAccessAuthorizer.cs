namespace MemorySystem.Application.Access;

public interface IMemoryAccessAuthorizer
{
    Task<MemoryAccessDecision> PreviewAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        return AuthorizeAsync(request, cancellationToken);
    }

    Task<MemoryAccessDecision> AuthorizeAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken = default);
}
