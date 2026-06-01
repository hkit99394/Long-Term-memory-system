using System.Security.Claims;
using System.Text.Encodings.Web;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace MemorySystem.Api.Authentication;

public sealed class OidcAuthenticationHandler : AuthenticationHandler<OidcAuthenticationOptions>
{
    private readonly OidcJwtValidator jwtValidator;
    private readonly IPrincipalResolver principalResolver;
    private readonly IAuthenticationAuditRecorder authenticationAuditRecorder;

    public OidcAuthenticationHandler(
        IOptionsMonitor<OidcAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        OidcJwtValidator jwtValidator,
        IPrincipalResolver principalResolver,
        IAuthenticationAuditRecorder authenticationAuditRecorder)
        : base(options, logger, encoder)
    {
        this.jwtValidator = jwtValidator;
        this.principalResolver = principalResolver;
        this.authenticationAuditRecorder = authenticationAuditRecorder;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Options.Enabled)
        {
            return AuthenticateResult.NoResult();
        }

        if (!Request.Headers.TryGetValue("Authorization", out var authorizationValues))
        {
            return AuthenticateResult.NoResult();
        }

        if (authorizationValues.Count != 1)
        {
            await RecordFailedAuthenticationAsync("invalid_bearer_header");
            return AuthenticateResult.Fail("Invalid bearer token.");
        }

        var token = ReadBearerToken(authorizationValues);
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            await RecordFailedAuthenticationAsync("blank_bearer_token");
            return AuthenticateResult.Fail("Invalid bearer token.");
        }

        var tokenValidation = await jwtValidator.ValidateAsync(token, Options, Context.RequestAborted);
        if (!tokenValidation.Succeeded)
        {
            await RecordFailedAuthenticationAsync("invalid_bearer_token");
            return AuthenticateResult.Fail("Invalid bearer token.");
        }

        var resolvedPrincipal = await principalResolver.ResolveIdentityBindingAsync(
            new IdentityBindingLookup(
                OidcAuthenticationDefaults.Provider,
                tokenValidation.Issuer!,
                tokenValidation.Subject!),
            AuthenticationMethods.Oidc,
            Context.RequestAborted);

        if (resolvedPrincipal is null
            || !string.Equals(resolvedPrincipal.PrincipalType, "human", StringComparison.Ordinal))
        {
            await RecordFailedAuthenticationAsync("unbound_or_non_human_oidc_subject");
            return AuthenticateResult.Fail("Invalid bearer token.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, resolvedPrincipal.PrincipalId.ToString("D")),
            new(ClaimTypes.Name, resolvedPrincipal.DisplayName),
            new(MemorySystemClaimTypes.PrincipalId, resolvedPrincipal.PrincipalId.ToString("D")),
            new(MemorySystemClaimTypes.PrincipalType, resolvedPrincipal.PrincipalType),
            new(MemorySystemClaimTypes.AuthMethod, resolvedPrincipal.AuthMethod),
            new(MemorySystemClaimTypes.CredentialId, resolvedPrincipal.CredentialId),
            new(MemorySystemClaimTypes.ExternalIssuer, resolvedPrincipal.ExternalIssuer ?? tokenValidation.Issuer!),
            new(MemorySystemClaimTypes.ExternalSubject, resolvedPrincipal.ExternalSubject ?? tokenValidation.Subject!)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        await RecordSucceededAuthenticationAsync(resolvedPrincipal);

        return AuthenticateResult.Success(ticket);
    }

    private Task RecordFailedAuthenticationAsync(string reasonCode)
    {
        return authenticationAuditRecorder.RecordAuthenticationAsync(
            Context,
            Scheme.Name,
            AccessAuditOutcomes.Failed,
            authMethod: AuthenticationMethods.Oidc,
            reasonCode: reasonCode,
            cancellationToken: Context.RequestAborted);
    }

    private Task RecordSucceededAuthenticationAsync(AuthenticatedPrincipal principal)
    {
        var identityBindingId = Guid.TryParse(principal.CredentialId, out var parsedIdentityBindingId)
            ? parsedIdentityBindingId
            : (Guid?)null;

        return authenticationAuditRecorder.RecordAuthenticationAsync(
            Context,
            Scheme.Name,
            AccessAuditOutcomes.Succeeded,
            principal.PrincipalId,
            principal.PrincipalType,
            principal.AuthMethod,
            principal.CredentialId,
            identityBindingId,
            cancellationToken: Context.RequestAborted);
    }

    private static string? ReadBearerToken(StringValues authorizationValues)
    {
        var authorizationHeader = authorizationValues[0];
        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorizationHeader["Bearer ".Length..].Trim();
    }
}
