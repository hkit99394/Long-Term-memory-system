using MemorySystem.Application.Authentication;
using MemorySystem.Application.AccessAuditing;
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
    private readonly IPrincipalResolver principalResolver;
    private readonly IAuthenticationAuditRecorder authenticationAuditRecorder;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IPrincipalResolver principalResolver,
        IAuthenticationAuditRecorder authenticationAuditRecorder)
        : base(options, logger, encoder)
    {
        this.principalResolver = principalResolver;
        this.authenticationAuditRecorder = authenticationAuditRecorder;
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
            await RecordFailedAuthenticationAsync("invalid_api_key_header");
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var providedKey = headerValues[0];
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            await RecordFailedAuthenticationAsync("blank_api_key");
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var credential = FindCredential(providedKey, configuredKeys);
        if (credential is null)
        {
            await RecordFailedAuthenticationAsync("unknown_api_key");
            return AuthenticateResult.Fail("Invalid API key.");
        }

        if (!Guid.TryParse(credential.PrincipalId, out var principalId))
        {
            await RecordFailedAuthenticationAsync("invalid_api_key_principal", credential.KeyId);
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var resolvedPrincipal = await principalResolver.ResolveApiKeyAsync(
            new ApiKeyPrincipalResolutionRequest(
                principalId,
                credential.KeyId,
                credential.DisplayName,
                credential.ServiceCredentialId),
            Context.RequestAborted);

        if (resolvedPrincipal is null)
        {
            await RecordFailedAuthenticationAsync(
                "inactive_api_key_principal_or_credential",
                credential.KeyId,
                principalId);
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, resolvedPrincipal.PrincipalId.ToString("D")),
            new(ClaimTypes.Name, resolvedPrincipal.DisplayName),
            new(MemorySystemClaimTypes.PrincipalId, resolvedPrincipal.PrincipalId.ToString("D")),
            new(MemorySystemClaimTypes.PrincipalType, resolvedPrincipal.PrincipalType),
            new(MemorySystemClaimTypes.AuthMethod, resolvedPrincipal.AuthMethod),
            new(MemorySystemClaimTypes.CredentialId, resolvedPrincipal.CredentialId),
            new(ApiKeyAuthenticationDefaults.ApiKeyIdClaimType, credential.KeyId)
        };

        if (!string.IsNullOrWhiteSpace(resolvedPrincipal.ExternalIssuer))
        {
            claims.Add(new Claim(MemorySystemClaimTypes.ExternalIssuer, resolvedPrincipal.ExternalIssuer));
        }

        if (!string.IsNullOrWhiteSpace(resolvedPrincipal.ExternalSubject))
        {
            claims.Add(new Claim(MemorySystemClaimTypes.ExternalSubject, resolvedPrincipal.ExternalSubject));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        await RecordSucceededAuthenticationAsync(resolvedPrincipal);

        return AuthenticateResult.Success(ticket);
    }

    private Task RecordFailedAuthenticationAsync(
        string reasonCode,
        string? credentialId = null,
        Guid? principalId = null)
    {
        return authenticationAuditRecorder.RecordAuthenticationAsync(
            Context,
            Scheme.Name,
            AccessAuditOutcomes.Failed,
            principalId,
            authMethod: AuthenticationMethods.ApiKey,
            credentialId: credentialId,
            reasonCode: reasonCode,
            cancellationToken: Context.RequestAborted);
    }

    private Task RecordSucceededAuthenticationAsync(AuthenticatedPrincipal principal)
    {
        return authenticationAuditRecorder.RecordAuthenticationAsync(
            Context,
            Scheme.Name,
            AccessAuditOutcomes.Succeeded,
            principal.PrincipalId,
            principal.PrincipalType,
            principal.AuthMethod,
            principal.CredentialId,
            cancellationToken: Context.RequestAborted);
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
                    credential.CredentialId,
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

    private sealed record ApiKeyCredential(
        string KeyId,
        string PrincipalId,
        string? ServiceCredentialId,
        string DisplayName);
}
