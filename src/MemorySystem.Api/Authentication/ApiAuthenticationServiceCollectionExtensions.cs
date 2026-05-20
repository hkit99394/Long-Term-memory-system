using Microsoft.AspNetCore.Authorization;
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
                ApiKeyAuthenticationOptions.HasDistinctKeyValues,
                "Authentication:ApiKey:Keys must not contain duplicate Key values.")
            .Validate(
                options => ApiKeyAuthenticationOptions.AllowsMissingKeys(environment.EnvironmentName)
                    || ApiKeyAuthenticationOptions.HasConfiguredKeys(options),
                "Authentication:ApiKey:Keys must contain at least one configured key outside Development and Testing environments.")
            .ValidateOnStart();

        services.AddSingleton<IApiKeyPrincipalValidator, PostgresApiKeyPrincipalValidator>();

        services
            .AddAuthentication(ApiKeyAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                _ => { });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder(ApiKeyAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
