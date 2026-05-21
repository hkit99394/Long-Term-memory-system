using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace MemorySystem.Infrastructure.Health;

public sealed class PostgresHealthCheck : IHealthCheck
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private const int CommandTimeoutSeconds = 3;

    private readonly NpgsqlDataSource dataSource;

    public PostgresHealthCheck(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            command.CommandTimeout = CommandTimeoutSeconds;

            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", exception);
        }
    }

}
