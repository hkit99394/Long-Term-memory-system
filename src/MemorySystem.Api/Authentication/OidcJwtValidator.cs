using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MemorySystem.Api.Authentication;

public sealed class OidcJwtValidator(IOidcJwksProvider jwksProvider)
{
    public async Task<OidcJwtValidationResult> ValidateAsync(
        string token,
        OidcAuthenticationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return OidcJwtValidationResult.Fail("Bearer token is required.");
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return OidcJwtValidationResult.Fail("Bearer token is malformed.");
        }

        JsonDocument header;
        JsonDocument payload;
        try
        {
            header = JsonDocument.Parse(Base64UrlDecode(parts[0]));
            payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        }
        catch (JsonException)
        {
            return OidcJwtValidationResult.Fail("Bearer token is not valid JSON.");
        }
        catch (FormatException)
        {
            return OidcJwtValidationResult.Fail("Bearer token is not base64url encoded.");
        }

        using (header)
        using (payload)
        {
            var algorithm = ReadString(header.RootElement, "alg");
            var keyId = ReadString(header.RootElement, "kid");

            if (!string.Equals(algorithm, "RS256", StringComparison.Ordinal))
            {
                return OidcJwtValidationResult.Fail("Bearer token algorithm is not supported.");
            }

            if (string.IsNullOrWhiteSpace(keyId))
            {
                return OidcJwtValidationResult.Fail("Bearer token is missing a key id.");
            }

            OidcJwksDocument jwks;
            try
            {
                jwks = await jwksProvider.GetJwksAsync(options, cancellationToken);
            }
            catch (Exception exception) when (IsJwksProviderFailure(exception, cancellationToken))
            {
                return OidcJwtValidationResult.Fail("Bearer token signing keys are unavailable.");
            }

            var key = jwks.Keys.FirstOrDefault(candidate =>
                string.Equals(candidate.KeyId, keyId, StringComparison.Ordinal)
                && string.Equals(candidate.KeyType, "RSA", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(candidate.Algorithm)
                    || string.Equals(candidate.Algorithm, "RS256", StringComparison.Ordinal)));

            if (key is null)
            {
                return OidcJwtValidationResult.Fail("Bearer token signing key is not trusted.");
            }

            if (!VerifySignature(parts[0], parts[1], parts[2], key))
            {
                return OidcJwtValidationResult.Fail("Bearer token signature is invalid.");
            }

            var issuer = ReadString(payload.RootElement, "iss");
            if (!string.Equals(issuer, options.Issuer, StringComparison.Ordinal))
            {
                return OidcJwtValidationResult.Fail("Bearer token issuer is not trusted.");
            }

            if (!HasAudience(payload.RootElement, options.Audience))
            {
                return OidcJwtValidationResult.Fail("Bearer token audience is not allowed.");
            }

            var subject = ReadString(payload.RootElement, "sub");
            if (string.IsNullOrWhiteSpace(subject))
            {
                return OidcJwtValidationResult.Fail("Bearer token subject is missing.");
            }

            if (!ValidateLifetime(payload.RootElement, options.ClockSkewSeconds, out var lifetimeError))
            {
                return OidcJwtValidationResult.Fail(lifetimeError);
            }

            return OidcJwtValidationResult.Success(issuer!, subject);
        }
    }

    private static bool VerifySignature(
        string encodedHeader,
        string encodedPayload,
        string encodedSignature,
        OidcJsonWebKey key)
    {
        try
        {
            var parameters = new RSAParameters
            {
                Modulus = Base64UrlDecode(key.Modulus),
                Exponent = Base64UrlDecode(key.Exponent)
            };

            using var rsa = RSA.Create();
            rsa.ImportParameters(parameters);

            var signingInput = Encoding.ASCII.GetBytes($"{encodedHeader}.{encodedPayload}");
            var signature = Base64UrlDecode(encodedSignature);

            return rsa.VerifyData(
                signingInput,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool ValidateLifetime(
        JsonElement payload,
        int clockSkewSeconds,
        out string error)
    {
        var now = DateTimeOffset.UtcNow;
        var clockSkew = TimeSpan.FromSeconds(clockSkewSeconds);

        if (!TryReadUnixTime(payload, "exp", out var expiresAt))
        {
            error = "Bearer token expiration is missing.";
            return false;
        }

        if (now > expiresAt + clockSkew)
        {
            error = "Bearer token is expired.";
            return false;
        }

        if (TryReadUnixTime(payload, "nbf", out var notBefore)
            && now + clockSkew < notBefore)
        {
            error = "Bearer token is not yet valid.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsJwksProviderFailure(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is HttpRequestException
            or JsonException
            or InvalidOperationException
            or NotSupportedException
            or OperationCanceledException;
    }

    private static bool TryReadUnixTime(JsonElement payload, string propertyName, out DateTimeOffset value)
    {
        value = default;

        if (!payload.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        long seconds;
        if (property.ValueKind == JsonValueKind.Number)
        {
            if (!property.TryGetInt64(out seconds))
            {
                return false;
            }
        }
        else if (property.ValueKind == JsonValueKind.String)
        {
            if (!long.TryParse(property.GetString(), out seconds))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        value = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return true;
    }

    private static bool HasAudience(JsonElement payload, string expectedAudience)
    {
        if (!payload.TryGetProperty("aud", out var audience))
        {
            return false;
        }

        return audience.ValueKind switch
        {
            JsonValueKind.String => string.Equals(audience.GetString(), expectedAudience, StringComparison.Ordinal),
            JsonValueKind.Array => audience
                .EnumerateArray()
                .Any(item => item.ValueKind == JsonValueKind.String
                    && string.Equals(item.GetString(), expectedAudience, StringComparison.Ordinal)),
            _ => false
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');

        return Convert.FromBase64String(padded);
    }
}

public sealed record OidcJwtValidationResult(
    bool Succeeded,
    string? Issuer,
    string? Subject,
    string? Error)
{
    public static OidcJwtValidationResult Success(string issuer, string subject)
    {
        return new OidcJwtValidationResult(true, issuer, subject, null);
    }

    public static OidcJwtValidationResult Fail(string error)
    {
        return new OidcJwtValidationResult(false, null, null, error);
    }
}
