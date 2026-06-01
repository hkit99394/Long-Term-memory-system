namespace MemorySystem.Api.Authentication;

public sealed record OidcJwksDocument(IReadOnlyList<OidcJsonWebKey> Keys);

public sealed record OidcJsonWebKey(
    string KeyId,
    string KeyType,
    string Algorithm,
    string Modulus,
    string Exponent);
