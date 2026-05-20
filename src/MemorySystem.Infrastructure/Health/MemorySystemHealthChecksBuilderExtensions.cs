using MemorySystem.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace MemorySystem.Infrastructure.Health;

public static class MemorySystemHealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddMemorySystemPostgres(this IHealthChecksBuilder builder)
    {
        return builder.Add(new HealthCheckRegistration(
            "postgres",
            serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
                var connectionString = PostgresConnectionString.Resolve(
                    key => configuration[key],
                    configuration.GetConnectionString("Postgres"),
                    environment.EnvironmentName);

                return new PostgresHealthCheck(connectionString);
            },
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "postgres", "ready"],
            timeout: PostgresHealthCheck.Timeout));
    }
}
