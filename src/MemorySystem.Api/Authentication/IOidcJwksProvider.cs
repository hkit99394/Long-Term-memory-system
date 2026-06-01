namespace MemorySystem.Api.Authentication;

public interface IOidcJwksProvider
{
    Task<OidcJwksDocument> GetJwksAsync(
        OidcAuthenticationOptions options,
        CancellationToken cancellationToken = default);
}
