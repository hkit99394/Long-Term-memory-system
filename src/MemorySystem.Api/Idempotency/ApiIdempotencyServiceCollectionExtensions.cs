using MemorySystem.Infrastructure.Configuration;
using MemorySystem.Infrastructure.Idempotency;
using Microsoft.Extensions.Options;

namespace MemorySystem.Api.Idempotency;

public static class ApiIdempotencyServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemApiIdempotency(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<ApiIdempotencyOptions>()
            .Bind(configuration.GetSection(ApiIdempotencyOptions.SectionName))
            .Validate(ApiIdempotencyOptions.IsValid, "API idempotency options are invalid.")
            .ValidateOnStart();

        services.AddSingleton<IApiRequestHasher, Sha256ApiRequestHasher>();
        services.AddSingleton<IApiIdempotencyStore>(_ =>
        {
            var connectionString = PostgresConnectionString.Resolve(
                key => configuration[key],
                configuration.GetConnectionString("Postgres"),
                environment.EnvironmentName);

            return new PostgresApiIdempotencyStore(connectionString);
        });
        services.AddSingleton<ApiIdempotencyHttpService>();

        return services;
    }
}
