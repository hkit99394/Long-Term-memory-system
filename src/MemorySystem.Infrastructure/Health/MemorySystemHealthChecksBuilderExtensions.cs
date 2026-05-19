using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MemorySystem.Infrastructure.Health;

public static class MemorySystemHealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddMemorySystemPostgres(
        this IHealthChecksBuilder builder,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.Add(new HealthCheckRegistration(
            "postgres",
            _ => new PostgresHealthCheck(connectionString),
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "postgres", "ready"]));
    }
}
