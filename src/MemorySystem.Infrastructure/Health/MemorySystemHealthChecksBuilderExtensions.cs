using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace MemorySystem.Infrastructure.Health;

public static class MemorySystemHealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddMemorySystemPostgres(this IHealthChecksBuilder builder)
    {
        return builder.Add(new HealthCheckRegistration(
            "postgres",
            serviceProvider =>
            {
                return new PostgresHealthCheck(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            },
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "postgres", "ready"],
            timeout: PostgresHealthCheck.Timeout));
    }
}
