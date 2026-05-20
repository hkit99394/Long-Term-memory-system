using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace MemorySystem.Api.Authentication;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyPrincipalValidator principalValidator;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyPrincipalValidator principalValidator)
        : base(options, logger, encoder)
    {
        this.principalValidator = principalValidator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKeys = Options.Keys;
        if (configuredKeys is null || configuredKeys.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        if (string.IsNullOrWhiteSpace(Options.HeaderName))
        {
            return AuthenticateResult.Fail("API key header name is not configured.");
        }

        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        if (headerValues.Count != 1)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var providedKey = headerValues[0];
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var credential = FindCredential(providedKey, configuredKeys);
        if (credential is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        if (!Guid.TryParse(credential.PrincipalId, out var principalId))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        try
        {
            if (!await principalValidator.IsActiveAsync(principalId, Context.RequestAborted))
            {
                return AuthenticateResult.Fail("Invalid API key.");
            }
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "API key principal validation failed.");
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, credential.PrincipalId),
            new Claim(ClaimTypes.Name, credential.DisplayName),
            new Claim(ApiKeyAuthenticationDefaults.ApiKeyIdClaimType, credential.KeyId)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private static ApiKeyCredential? FindCredential(
        string providedKey,
        IEnumerable<KeyValuePair<string, ApiKeyCredentialOptions>> configuredKeys)
    {
        foreach (var (keyId, credential) in configuredKeys)
        {
            if (string.IsNullOrWhiteSpace(credential.Key) || string.IsNullOrWhiteSpace(credential.PrincipalId))
            {
                continue;
            }

            if (ApiKeysMatch(providedKey, credential.Key))
            {
                return new ApiKeyCredential(
                    keyId,
                    credential.PrincipalId,
                    string.IsNullOrWhiteSpace(credential.DisplayName)
                        ? credential.PrincipalId
                        : credential.DisplayName);
            }
        }

        return null;
    }

    private static bool ApiKeysMatch(string providedKey, string configuredKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);

        return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }

    private sealed record ApiKeyCredential(string KeyId, string PrincipalId, string DisplayName);
}
