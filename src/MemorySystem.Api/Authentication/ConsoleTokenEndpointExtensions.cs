using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MemorySystem.Api.Authentication;

public static class ConsoleTokenEndpointExtensions
{
    public const string LoginPath = "/auth/login";

    private const string LoginTitle = "Console token login is invalid.";

    public static IEndpointRouteBuilder MapMemorySystemConsoleTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                LoginPath,
                (HttpContext context) => Results.Content(
                    BuildLoginPage(ReadReturnUrl(context)),
                    "text/html; charset=utf-8"))
            .AllowAnonymous()
            .DisableRateLimiting();

        endpoints.MapPost(
                "/api/auth/console/oidc-token",
                async (
                    HttpContext context,
                    IOptionsMonitor<OidcAuthenticationOptions> oidcOptions,
                    OidcJwtValidator jwtValidator,
                    IPrincipalResolver principalResolver,
                    IAuthenticationAuditRecorder authenticationAuditRecorder,
                    CancellationToken cancellationToken) =>
                    await ValidateOidcTokenAsync(
                        context,
                        oidcOptions,
                        jwtValidator,
                        principalResolver,
                        authenticationAuditRecorder,
                        cancellationToken))
            .AllowAnonymous();

        endpoints.MapPost(
                "/api/auth/console/break-glass-key",
                async (
                    HttpContext context,
                    IOptionsMonitor<ApiKeyAuthenticationOptions> apiKeyOptions,
                    IPrincipalResolver principalResolver,
                    IAuthenticationAuditRecorder authenticationAuditRecorder,
                    CancellationToken cancellationToken) =>
                    await ValidateBreakGlassKeyAsync(
                        context,
                        apiKeyOptions,
                        principalResolver,
                        authenticationAuditRecorder,
                        cancellationToken))
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ValidateOidcTokenAsync(
        HttpContext context,
        IOptionsMonitor<OidcAuthenticationOptions> oidcOptions,
        OidcJwtValidator jwtValidator,
        IPrincipalResolver principalResolver,
        IAuthenticationAuditRecorder authenticationAuditRecorder,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<ConsoleOidcTokenRequest>(
            context.Request,
            LoginTitle,
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return ToResult(requestResult.Problem!);
        }

        var request = requestResult.Value!;
        var returnUrl = NormalizeReturnUrl(request.ReturnUrl);
        var token = request.Token?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.Oidc,
                "blank_oidc_token",
                cancellationToken: cancellationToken);

            return Problem(
                StatusCodes.Status400BadRequest,
                LoginTitle,
                "OIDC JWT is required.");
        }

        var options = oidcOptions.Get(OidcAuthenticationDefaults.AuthenticationScheme);
        if (!options.Enabled)
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.Oidc,
                "oidc_disabled",
                cancellationToken: cancellationToken);

            return Problem(
                StatusCodes.Status503ServiceUnavailable,
                "OIDC login is unavailable.",
                "OIDC authentication is not enabled for this environment.");
        }

        var tokenValidation = await jwtValidator.ValidateAsync(token, options, cancellationToken);
        if (!tokenValidation.Succeeded)
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.Oidc,
                "invalid_oidc_token",
                cancellationToken: cancellationToken);

            return Problem(
                StatusCodes.Status401Unauthorized,
                LoginTitle,
                "OIDC JWT is invalid.");
        }

        var resolvedPrincipal = await principalResolver.ResolveIdentityBindingAsync(
            new IdentityBindingLookup(
                OidcAuthenticationDefaults.Provider,
                tokenValidation.Issuer!,
                tokenValidation.Subject!),
            AuthenticationMethods.Oidc,
            cancellationToken);

        if (resolvedPrincipal is null
            || !string.Equals(resolvedPrincipal.PrincipalType, "human", StringComparison.Ordinal))
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.Oidc,
                "unbound_or_non_human_oidc_subject",
                cancellationToken: cancellationToken);

            return Problem(
                StatusCodes.Status401Unauthorized,
                LoginTitle,
                "OIDC JWT is not bound to an active human principal.");
        }

        await RecordSucceededAuthenticationAsync(authenticationAuditRecorder, context, resolvedPrincipal, cancellationToken);

        return Results.Ok(ToTokenResponse(resolvedPrincipal, "oidc_jwt", returnUrl));
    }

    private static async Task<IResult> ValidateBreakGlassKeyAsync(
        HttpContext context,
        IOptionsMonitor<ApiKeyAuthenticationOptions> apiKeyOptions,
        IPrincipalResolver principalResolver,
        IAuthenticationAuditRecorder authenticationAuditRecorder,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<ConsoleBreakGlassKeyRequest>(
            context.Request,
            LoginTitle,
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return ToResult(requestResult.Problem!);
        }

        var request = requestResult.Value!;
        var returnUrl = NormalizeReturnUrl(request.ReturnUrl);
        var apiKey = request.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.ApiKey,
                "blank_api_key",
                cancellationToken: cancellationToken);

            return Problem(
                StatusCodes.Status400BadRequest,
                LoginTitle,
                "Break-glass API key is required.");
        }

        var options = apiKeyOptions.Get(ApiKeyAuthenticationDefaults.AuthenticationScheme);
        var credential = FindApiKeyCredential(apiKey, options);
        if (credential is null)
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.ApiKey,
                "unknown_api_key",
                cancellationToken: cancellationToken);

            return Problem(
                StatusCodes.Status401Unauthorized,
                LoginTitle,
                "Break-glass API key is invalid.");
        }

        var resolvedPrincipal = await principalResolver.ResolveApiKeyAsync(
            new ApiKeyPrincipalResolutionRequest(
                credential.PrincipalId,
                credential.KeyId,
                credential.DisplayName,
                credential.CredentialId),
            cancellationToken);

        if (resolvedPrincipal is null
            || !string.Equals(resolvedPrincipal.PrincipalType, "human", StringComparison.Ordinal))
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.ApiKey,
                "inactive_or_non_human_api_key_principal",
                credential.KeyId,
                credential.PrincipalId,
                cancellationToken);

            return Problem(
                StatusCodes.Status401Unauthorized,
                LoginTitle,
                "Break-glass API key is not an active human principal.");
        }

        await RecordSucceededAuthenticationAsync(authenticationAuditRecorder, context, resolvedPrincipal, cancellationToken);

        return Results.Ok(ToTokenResponse(resolvedPrincipal, "break_glass_api_key", returnUrl));
    }

    private static ConsoleTokenResponse ToTokenResponse(
        AuthenticatedPrincipal resolvedPrincipal,
        string credentialKind,
        string returnUrl)
    {
        return new ConsoleTokenResponse(
            Authenticated: true,
            PrincipalId: resolvedPrincipal.PrincipalId.ToString("D"),
            DisplayName: resolvedPrincipal.DisplayName,
            PrincipalType: resolvedPrincipal.PrincipalType,
            AuthMethod: resolvedPrincipal.AuthMethod,
            CredentialId: resolvedPrincipal.CredentialId,
            CredentialKind: credentialKind,
            ReturnUrl: returnUrl);
    }

    private static ApiKeyCredential? FindApiKeyCredential(
        string providedKey,
        ApiKeyAuthenticationOptions options)
    {
        foreach (var (keyId, credential) in options.Keys)
        {
            if (string.IsNullOrWhiteSpace(keyId)
                || credential is null
                || string.IsNullOrWhiteSpace(credential.Key)
                || !Guid.TryParse(credential.PrincipalId, out var principalId))
            {
                continue;
            }

            if (!ApiKeysMatch(providedKey, credential.Key))
            {
                continue;
            }

            return new ApiKeyCredential(
                keyId,
                principalId,
                credential.CredentialId,
                string.IsNullOrWhiteSpace(credential.DisplayName)
                    ? principalId.ToString("D")
                    : credential.DisplayName);
        }

        return null;
    }

    private static bool ApiKeysMatch(string providedKey, string configuredKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);

        return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }

    private static Task RecordSucceededAuthenticationAsync(
        IAuthenticationAuditRecorder authenticationAuditRecorder,
        HttpContext context,
        AuthenticatedPrincipal principal,
        CancellationToken cancellationToken)
    {
        var identityBindingId = string.Equals(principal.AuthMethod, AuthenticationMethods.Oidc, StringComparison.Ordinal)
            && Guid.TryParse(principal.CredentialId, out var parsedIdentityBindingId)
                ? parsedIdentityBindingId
                : (Guid?)null;

        return authenticationAuditRecorder.RecordAuthenticationAsync(
            context,
            "ConsoleToken",
            AccessAuditOutcomes.Succeeded,
            principal.PrincipalId,
            principal.PrincipalType,
            principal.AuthMethod,
            principal.CredentialId,
            identityBindingId,
            cancellationToken: cancellationToken);
    }

    private static Task RecordFailedAuthenticationAsync(
        IAuthenticationAuditRecorder authenticationAuditRecorder,
        HttpContext context,
        string authMethod,
        string reasonCode,
        string? credentialId = null,
        Guid? principalId = null,
        CancellationToken cancellationToken = default)
    {
        return authenticationAuditRecorder.RecordAuthenticationAsync(
            context,
            "ConsoleToken",
            AccessAuditOutcomes.Failed,
            principalId,
            authMethod: authMethod,
            credentialId: credentialId,
            reasonCode: reasonCode,
            cancellationToken: cancellationToken);
    }

    private static IResult Problem(int statusCode, string title, string detail)
    {
        return Results.Problem(statusCode: statusCode, title: title, detail: detail);
    }

    private static IResult ToResult(ApiIdempotencyResponse response)
    {
        return response.Body is ProblemDetails problem
            ? Results.Problem(
                statusCode: response.StatusCode,
                title: problem.Title,
                detail: problem.Detail)
            : Results.Json(response.Body, statusCode: response.StatusCode, contentType: response.ContentType);
    }

    private static string ReadReturnUrl(HttpContext context)
    {
        return NormalizeReturnUrl(context.Request.Query["returnUrl"].ToString());
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/admin/";
        }

        return returnUrl.StartsWith("/", StringComparison.Ordinal)
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.Contains("\\", StringComparison.Ordinal)
                ? returnUrl
                : "/admin/";
    }

    private static string BuildLoginPage(string returnUrl)
    {
        var encodedReturnUrl = HtmlEncoder.Default.Encode(returnUrl);

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Memory Console Login</title>
              <style>
                :root { color-scheme: light dark; font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #f5f7fb; color: #1f2937; }
                main { width: min(92vw, 440px); background: #fff; border: 1px solid #d9e2ef; border-radius: 8px; padding: 24px; box-shadow: 0 16px 48px rgb(15 23 42 / 10%); }
                h1 { margin: 0 0 18px; font-size: 1.35rem; line-height: 1.2; }
                label, textarea, input, button { display: block; width: 100%; box-sizing: border-box; }
                label { margin-top: 14px; font-size: 0.8rem; font-weight: 700; text-transform: uppercase; color: #4b5563; }
                textarea, input { margin-top: 6px; border: 1px solid #c8d4e3; border-radius: 6px; padding: 10px; font: inherit; }
                textarea { min-height: 112px; resize: vertical; }
                button { margin-top: 12px; border: 0; border-radius: 6px; padding: 10px 12px; font-weight: 700; cursor: pointer; background: #2563eb; color: #fff; }
                button.secondary { background: #334155; }
                #status { min-height: 1.25rem; margin-top: 14px; color: #b91c1c; font-size: 0.9rem; }
                @media (prefers-color-scheme: dark) {
                  body { background: #0f172a; color: #e5e7eb; }
                  main { background: #111827; border-color: #334155; box-shadow: none; }
                  label { color: #cbd5e1; }
                  textarea, input { background: #0b1220; color: #f8fafc; border-color: #475569; }
                }
              </style>
            </head>
            <body>
              <main>
                <h1>Memory Console Login</h1>
                <form id="oidc-form" autocomplete="off">
                  <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}">
                  <label>
                    OIDC JWT
                    <textarea name="token" required spellcheck="false"></textarea>
                  </label>
                  <button type="submit">Use JWT</button>
                </form>
                <form id="api-key-form" autocomplete="off">
                  <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}">
                  <label>
                    Break-glass API key
                    <input name="apiKey" type="password">
                  </label>
                  <button class="secondary" type="submit">Use break-glass key</button>
                </form>
                <div id="status" role="status"></div>
              </main>
              <script>
                const credentialKey = "memorySystem.consoleCredential";
                const credentialKindKey = "memorySystem.consoleCredentialKind";
                const status = document.getElementById("status");
                document.getElementById("oidc-form").addEventListener("submit", event => {
                  event.preventDefault();
                  void login("/api/auth/console/oidc-token", {
                    token: event.currentTarget.token.value,
                    returnUrl: event.currentTarget.returnUrl.value
                  }, event.currentTarget.token.value, "oidc_jwt");
                });
                document.getElementById("api-key-form").addEventListener("submit", event => {
                  event.preventDefault();
                  void login("/api/auth/console/break-glass-key", {
                    apiKey: event.currentTarget.apiKey.value,
                    returnUrl: event.currentTarget.returnUrl.value
                  }, event.currentTarget.apiKey.value, "break_glass_api_key");
                });
                async function login(path, body, credential, credentialKind) {
                  status.textContent = "";
                  const response = await fetch(path, {
                    method: "POST",
                    credentials: "same-origin",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(body)
                  });
                  const text = await response.text();
                  const payload = text ? JSON.parse(text) : {};
                  if (!response.ok) {
                    status.textContent = payload.detail || payload.title || response.statusText;
                    return;
                  }
                  sessionStorage.setItem(credentialKey, credential.trim());
                  sessionStorage.setItem(credentialKindKey, credentialKind);
                  window.location.assign(payload.returnUrl || "/admin/");
                }
              </script>
            </body>
            </html>
            """;
    }

    private sealed record ConsoleOidcTokenRequest(string? Token, string? ReturnUrl);

    private sealed record ConsoleBreakGlassKeyRequest(string? ApiKey, string? ReturnUrl);

    private sealed record ConsoleTokenResponse(
        bool Authenticated,
        string PrincipalId,
        string DisplayName,
        string PrincipalType,
        string AuthMethod,
        string CredentialId,
        string CredentialKind,
        string ReturnUrl);

    private sealed record ApiKeyCredential(
        string KeyId,
        Guid PrincipalId,
        string? CredentialId,
        string DisplayName);
}
