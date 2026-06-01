namespace MemorySystem.Application.Authentication;

public interface IIdentityBindingStore
{
    Task<IdentityBindingPrincipal?> FindActiveAsync(
        IdentityBindingLookup lookup,
        CancellationToken cancellationToken = default);
}
