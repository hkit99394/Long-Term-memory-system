using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace MemorySystem.Infrastructure.Health;

public sealed class PostgresHealthCheck : IHealthCheck
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private const int ConnectTimeoutSeconds = 3;
    private const int CommandTimeoutSeconds = 3;

    private readonly string connectionString;

    public PostgresHealthCheck(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        this.connectionString = BuildHealthCheckConnectionString(connectionString);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

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

    private static string BuildHealthCheckConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (builder.Timeout <= 0 || builder.Timeout > ConnectTimeoutSeconds)
        {
            builder.Timeout = ConnectTimeoutSeconds;
        }

        if (builder.CommandTimeout <= 0 || builder.CommandTimeout > CommandTimeoutSeconds)
        {
            builder.CommandTimeout = CommandTimeoutSeconds;
        }

        return builder.ConnectionString;
    }
}
