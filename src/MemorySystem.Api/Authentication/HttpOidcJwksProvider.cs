using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MemorySystem.Api.Authentication;

public sealed class HttpOidcJwksProvider(
    HttpClient httpClient,
    IMemoryCache memoryCache) : IOidcJwksProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<OidcJwksDocument> GetJwksAsync(
        OidcAuthenticationOptions options,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"oidc:jwks:{options.JwksUri}";
        if (memoryCache.TryGetValue(cacheKey, out OidcJwksDocument? cachedJwks)
            && cachedJwks is not null)
        {
            return cachedJwks;
        }

        var jwks = await FetchJwksAsync(options, cancellationToken);
        memoryCache.Set(cacheKey, jwks, CacheDuration);

        return jwks;
    }

    private async Task<OidcJwksDocument> FetchJwksAsync(
        OidcAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(options.JwksUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("keys", out var keysElement)
            || keysElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("OIDC JWKS document does not contain a keys array.");
        }

        var keys = new List<OidcJsonWebKey>();
        foreach (var keyElement in keysElement.EnumerateArray())
        {
            var keyType = ReadOptionalString(keyElement, "kty");
            var keyId = ReadOptionalString(keyElement, "kid");
            var algorithm = ReadOptionalString(keyElement, "alg") ?? string.Empty;
            var modulus = ReadOptionalString(keyElement, "n");
            var exponent = ReadOptionalString(keyElement, "e");

            if (string.IsNullOrWhiteSpace(keyType)
                || string.IsNullOrWhiteSpace(keyId)
                || string.IsNullOrWhiteSpace(modulus)
                || string.IsNullOrWhiteSpace(exponent))
            {
                continue;
            }

            keys.Add(new OidcJsonWebKey(keyId, keyType, algorithm, modulus, exponent));
        }

        return new OidcJwksDocument(keys);
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
