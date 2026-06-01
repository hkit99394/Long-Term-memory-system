namespace MemorySystem.Application.Authentication;

public interface IPrincipalResolver
{
    Task<AuthenticatedPrincipal?> ResolveApiKeyAsync(
        ApiKeyPrincipalResolutionRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedPrincipal?> ResolveIdentityBindingAsync(
        IdentityBindingLookup lookup,
        string authMethod = AuthenticationMethods.Oidc,
        CancellationToken cancellationToken = default);
}
