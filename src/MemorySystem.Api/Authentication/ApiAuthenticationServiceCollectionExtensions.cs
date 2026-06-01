using MemorySystem.Application.Authentication;
using MemorySystem.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MemorySystem.Api.Authentication;

public static class ApiAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<ApiKeyAuthenticationOptions>(ApiKeyAuthenticationDefaults.AuthenticationScheme)
            .Bind(configuration.GetSection(ApiKeyAuthenticationOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.HeaderName), "API key header name must be configured.")
            .Validate(
                ApiKeyAuthenticationOptions.HasOnlyConfiguredKeys,
                "Every Authentication:ApiKey:Keys entry must configure Key and PrincipalId.")
            .Validate(
                ApiKeyAuthenticationOptions.HasValidPrincipalIds,
                "Every Authentication:ApiKey:Keys PrincipalId must be a valid GUID.")
            .Validate(
                ApiKeyAuthenticationOptions.HasValidServiceCredentialIds,
                "Every Authentication:ApiKey:Keys CredentialId must be a valid GUID when configured.")
            .Validate(
                ApiKeyAuthenticationOptions.HasDistinctKeyValues,
                "Authentication:ApiKey:Keys must not contain duplicate Key values.")
            .Validate(
                options => ApiKeyAuthenticationOptions.AllowsMissingKeys(environment.EnvironmentName)
                    || ApiKeyAuthenticationOptions.HasProductionSafeKeyValues(options),
                "Authentication:ApiKey:Keys values must be production-safe outside Development and Testing environments.")
            .Validate(
                options => ApiKeyAuthenticationOptions.AllowsMissingKeys(environment.EnvironmentName)
                    || ApiKeyAuthenticationOptions.HasConfiguredKeys(options),
                "Authentication:ApiKey:Keys must contain at least one configured key outside Development and Testing environments.")
            .ValidateOnStart();

        services
            .AddOptions<OidcAuthenticationOptions>(OidcAuthenticationDefaults.AuthenticationScheme)
            .Bind(configuration.GetSection(OidcAuthenticationOptions.SectionName))
            .Validate(
                OidcAuthenticationOptions.HasRequiredConfiguration,
                "Authentication:Oidc must configure Issuer, Audience, and JwksUri when enabled.")
            .Validate(
                OidcAuthenticationOptions.HasValidJwksUri,
                "Authentication:Oidc:JwksUri must be an absolute HTTP or HTTPS URI when OIDC is enabled.")
            .Validate(
                OidcAuthenticationOptions.HasValidClockSkew,
                "Authentication:Oidc:ClockSkewSeconds must be between 0 and 3600.")
            .Validate(
                options => OidcAuthenticationOptions.HasHttpsMetadataWhenRequired(options, environment.EnvironmentName),
                "Authentication:Oidc:JwksUri must use HTTPS when metadata HTTPS is required.")
            .ValidateOnStart();

        services.AddSingleton<IIdentityBindingStore, PostgresIdentityBindingStore>();
        services.AddSingleton<IPrincipalResolver, PostgresPrincipalResolver>();
        services.AddMemoryCache();
        services.AddHttpClient<IOidcJwksProvider, HttpOidcJwksProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddSingleton<OidcJwtValidator>();
        services.AddSingleton<IAuthenticationAuditRecorder, AccessAuditAuthenticationAuditRecorder>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationMiddlewareResultHandler>();

        services
            .AddAuthentication(ApiKeyAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                _ => { })
            .AddScheme<OidcAuthenticationOptions, OidcAuthenticationHandler>(
                OidcAuthenticationDefaults.AuthenticationScheme,
                _ => { });

        services.AddAuthorization(options =>
        {
            var authenticatedApiPolicy = new AuthorizationPolicyBuilder(
                    ApiKeyAuthenticationDefaults.AuthenticationScheme,
                    OidcAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();

            options.DefaultPolicy = authenticatedApiPolicy;
            options.FallbackPolicy = authenticatedApiPolicy;
        });

        return services;
    }
}
