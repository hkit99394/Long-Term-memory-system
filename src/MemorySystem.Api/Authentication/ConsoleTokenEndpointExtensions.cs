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
    public const string LogoutPath = "/auth/logout";

    private const string LoginTitle = "Console token login is invalid.";

    public static IEndpointRouteBuilder MapMemorySystemConsoleTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                LoginPath,
                (
                    HttpContext context,
                    IOptionsMonitor<ConsolePasswordLoginOptions> consolePasswordOptions,
                    IConfiguration configuration,
                    IHostEnvironment environment) => Results.Content(
                    BuildLoginPage(
                        ReadReturnUrl(context),
                        consolePasswordOptions.CurrentValue.Enabled,
                        ReadEnvironmentLabel(configuration, environment)),
                    "text/html; charset=utf-8"))
            .AllowAnonymous()
            .DisableRateLimiting();

        endpoints.MapGet(
                LogoutPath,
                (HttpContext context) => Results.Content(
                    BuildLogoutPage(ReadReturnUrl(context)),
                    "text/html; charset=utf-8"))
            .AllowAnonymous()
            .DisableRateLimiting();

        endpoints.MapPost(
                "/api/auth/console/password",
                async (
                    HttpContext context,
                    IOptionsMonitor<ConsolePasswordLoginOptions> consolePasswordOptions,
                    IOptionsMonitor<ApiKeyAuthenticationOptions> apiKeyOptions,
                    IPrincipalResolver principalResolver,
                    IAuthenticationAuditRecorder authenticationAuditRecorder,
                    CancellationToken cancellationToken) =>
                    await ValidateConsolePasswordAsync(
                        context,
                        consolePasswordOptions,
                        apiKeyOptions,
                        principalResolver,
                        authenticationAuditRecorder,
                        cancellationToken))
            .AllowAnonymous();

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

    private static async Task<IResult> ValidateConsolePasswordAsync(
        HttpContext context,
        IOptionsMonitor<ConsolePasswordLoginOptions> consolePasswordOptions,
        IOptionsMonitor<ApiKeyAuthenticationOptions> apiKeyOptions,
        IPrincipalResolver principalResolver,
        IAuthenticationAuditRecorder authenticationAuditRecorder,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<ConsolePasswordRequest>(
            context.Request,
            LoginTitle,
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return ToResult(requestResult.Problem!);
        }

        var request = requestResult.Value!;
        var returnUrl = NormalizeReturnUrl(request.ReturnUrl);
        var options = consolePasswordOptions.CurrentValue;
        if (!options.Enabled)
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.ApiKey,
                "console_password_disabled",
                cancellationToken: cancellationToken);

            return Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Password login is unavailable.",
                "Password login is not enabled for this environment.");
        }

        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(request.Password))
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.ApiKey,
                "blank_console_password_credentials",
                cancellationToken: cancellationToken);

            return Problem(
                StatusCodes.Status400BadRequest,
                LoginTitle,
                "Username and password are required.");
        }

        if (!ConfiguredCredentialsMatch(username, request.Password, options))
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.ApiKey,
                "invalid_console_password",
                cancellationToken: cancellationToken);

            return Problem(
                StatusCodes.Status401Unauthorized,
                LoginTitle,
                "Username or password is invalid.");
        }

        var apiKeyCredential = FindApiKeyCredentialById(
            options.ApiKeyId!.Trim(),
            apiKeyOptions.Get(ApiKeyAuthenticationDefaults.AuthenticationScheme));
        if (apiKeyCredential is null)
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.ApiKey,
                "console_password_api_key_not_configured",
                cancellationToken: cancellationToken);

            return Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Password login is unavailable.",
                "The configured password-login API key is not available.");
        }

        var resolvedPrincipal = await principalResolver.ResolveApiKeyAsync(
            new ApiKeyPrincipalResolutionRequest(
                apiKeyCredential.PrincipalId,
                apiKeyCredential.KeyId,
                apiKeyCredential.DisplayName,
                apiKeyCredential.CredentialId),
            cancellationToken);

        if (resolvedPrincipal is null
            || !string.Equals(resolvedPrincipal.PrincipalType, "human", StringComparison.Ordinal))
        {
            await RecordFailedAuthenticationAsync(
                authenticationAuditRecorder,
                context,
                AuthenticationMethods.ApiKey,
                "inactive_or_non_human_console_password_principal",
                apiKeyCredential.KeyId,
                apiKeyCredential.PrincipalId,
                cancellationToken);

            return Problem(
                StatusCodes.Status401Unauthorized,
                LoginTitle,
                "Password login is not bound to an active human principal.");
        }

        await RecordSucceededAuthenticationAsync(authenticationAuditRecorder, context, resolvedPrincipal, cancellationToken);

        return Results.Ok(ToTokenResponse(
            resolvedPrincipal,
            "console_password_api_key",
            returnUrl,
            apiKeyCredential.Key));
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
        string returnUrl,
        string? credential = null)
    {
        return new ConsoleTokenResponse(
            Authenticated: true,
            PrincipalId: resolvedPrincipal.PrincipalId.ToString("D"),
            DisplayName: resolvedPrincipal.DisplayName,
            PrincipalType: resolvedPrincipal.PrincipalType,
            AuthMethod: resolvedPrincipal.AuthMethod,
            CredentialId: resolvedPrincipal.CredentialId,
            CredentialKind: credentialKind,
            ReturnUrl: returnUrl,
            Credential: credential);
    }

    private static ApiKeyCredential? FindApiKeyCredentialById(
        string keyId,
        ApiKeyAuthenticationOptions options)
    {
        return options.Keys.TryGetValue(keyId, out var credential)
            ? ToApiKeyCredential(keyId, credential)
            : null;
    }

    private static ApiKeyCredential? FindApiKeyCredential(
        string providedKey,
        ApiKeyAuthenticationOptions options)
    {
        foreach (var (keyId, credential) in options.Keys)
        {
            var apiKeyCredential = ToApiKeyCredential(keyId, credential);
            if (apiKeyCredential is null)
            {
                continue;
            }

            if (!ApiKeysMatch(providedKey, apiKeyCredential.Key))
            {
                continue;
            }

            return apiKeyCredential;
        }

        return null;
    }

    private static ApiKeyCredential? ToApiKeyCredential(
        string keyId,
        ApiKeyCredentialOptions? credential)
    {
        if (string.IsNullOrWhiteSpace(keyId)
            || credential is null
            || string.IsNullOrWhiteSpace(credential.Key)
            || !Guid.TryParse(credential.PrincipalId, out var principalId))
        {
            return null;
        }

        return new ApiKeyCredential(
            keyId,
            credential.Key,
            principalId,
            credential.CredentialId,
            string.IsNullOrWhiteSpace(credential.DisplayName)
                ? principalId.ToString("D")
                : credential.DisplayName);
    }

    private static bool ConfiguredCredentialsMatch(
        string username,
        string password,
        ConsolePasswordLoginOptions options)
    {
        return ApiKeysMatch(username, options.Username!)
            && ApiKeysMatch(password, options.Password!);
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

    private static string ReadEnvironmentLabel(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredLabel = configuration["Authentication:ConsoleLogin:EnvironmentLabel"];

        return string.IsNullOrWhiteSpace(configuredLabel)
            ? environment.EnvironmentName
            : configuredLabel.Trim();
    }

    private static string BuildLoginPage(
        string returnUrl,
        bool consolePasswordLoginEnabled,
        string environmentLabel)
    {
        var encodedReturnUrl = HtmlEncoder.Default.Encode(returnUrl);
        var encodedEnvironmentLabel = HtmlEncoder.Default.Encode(environmentLabel);
        var passwordSection = consolePasswordLoginEnabled
            ? $$"""
                <section class="login-section primary" aria-labelledby="password-heading">
                  <div class="section-heading">
                    <p class="eyebrow">Operator access</p>
                    <h2 id="password-heading">Sign in</h2>
                  </div>
                  <form id="password-form" autocomplete="on">
                    <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}">
                    <label>
                      Username
                      <input name="username" autocomplete="username" required>
                    </label>
                    <label>
                      Password
                      <input name="password" type="password" autocomplete="current-password" required>
                    </label>
                    <button type="submit">Sign in</button>
                  </form>
                </section>
                """
            : string.Empty;
        var advancedOpenAttribute = consolePasswordLoginEnabled ? string.Empty : " open";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Memory Console Login</title>
              <link rel="stylesheet" href="/auth/login.css">
            </head>
            <body>
              <main class="login-shell">
                <section class="brand-panel" aria-labelledby="login-title">
                  <div class="brand-kicker">
                    <p class="eyebrow">Long Term Memory System</p>
                    <p class="environment-badge" aria-label="Environment">{{encodedEnvironmentLabel}}</p>
                  </div>
                  <h1 id="login-title">Memory Console Login</h1>
                  <p class="lede">Access is kept in this browser session only. Closing the tab or signing out clears the console credential.</p>
                </section>
                <section class="login-panel" aria-label="Console authentication">
                  {{passwordSection}}
                  <details class="advanced-access"{{advancedOpenAttribute}}>
                    <summary>Advanced access</summary>
                    <section class="login-section" aria-labelledby="oidc-heading">
                      <div class="section-heading">
                        <p class="eyebrow">Federated access</p>
                        <h2 id="oidc-heading">Operator token</h2>
                      </div>
                      <form id="oidc-form" autocomplete="off">
                        <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}">
                        <label>
                          Token
                          <textarea name="token" required spellcheck="false"></textarea>
                        </label>
                        <button class="secondary" type="submit">Use operator token</button>
                      </form>
                    </section>
                    <section class="login-section compact" aria-labelledby="api-key-heading">
                      <div class="section-heading">
                        <p class="eyebrow">Emergency access</p>
                        <h2 id="api-key-heading">Emergency access key</h2>
                        <p class="helper">Privileged and audited.</p>
                      </div>
                      <form id="api-key-form" autocomplete="off">
                        <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}">
                        <label>
                          Access key
                          <input name="apiKey" type="password" autocomplete="off">
                        </label>
                        <button class="secondary" type="submit">Use emergency key</button>
                      </form>
                    </section>
                  </details>
                </section>
                <div id="status" role="status" aria-live="polite" tabindex="-1"></div>
              </main>
              <script type="module" src="/auth/login.js"></script>
            </body>
            </html>
            """;
    }

    private static string BuildLogoutPage(string returnUrl)
    {
        var encodedReturnUrl = HtmlEncoder.Default.Encode(returnUrl);
        var encodedLoginUrl = HtmlEncoder.Default.Encode($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Signing out</title>
              <link rel="stylesheet" href="/auth/login.css">
            </head>
            <body>
              <main class="login-shell logout-shell">
                <section class="brand-panel" aria-labelledby="logout-title">
                  <p class="eyebrow">Long Term Memory System</p>
                  <h1 id="logout-title">Signing Out</h1>
                  <p class="lede">Clearing the browser console credential.</p>
                </section>
                <section class="login-panel" aria-label="Sign out status">
                  <p id="status" role="status" aria-live="polite" data-return-url="{{encodedReturnUrl}}">Signing out...</p>
                  <p class="helper"><a href="{{encodedLoginUrl}}">Return to login</a></p>
                </section>
              </main>
              <script type="module" src="/auth/logout.js"></script>
            </body>
            </html>
            """;
    }

    private sealed record ConsoleOidcTokenRequest(string? Token, string? ReturnUrl);

    private sealed record ConsoleBreakGlassKeyRequest(string? ApiKey, string? ReturnUrl);

    private sealed record ConsolePasswordRequest(string? Username, string? Password, string? ReturnUrl);

    private sealed record ConsoleTokenResponse(
        bool Authenticated,
        string PrincipalId,
        string DisplayName,
        string PrincipalType,
        string AuthMethod,
        string CredentialId,
        string CredentialKind,
        string ReturnUrl,
        string? Credential);

    private sealed record ApiKeyCredential(
        string KeyId,
        string Key,
        Guid PrincipalId,
        string? CredentialId,
        string DisplayName);
}
