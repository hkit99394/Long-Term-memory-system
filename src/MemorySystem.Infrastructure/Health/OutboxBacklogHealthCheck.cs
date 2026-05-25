using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace MemorySystem.Infrastructure.Health;

public sealed class OutboxBacklogHealthCheck(
    NpgsqlDataSource dataSource,
    OutboxBacklogHealthOptions options) : IHealthCheck
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private const int CommandTimeoutSeconds = 3;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await ReadSummaryAsync(cancellationToken);
            var data = new Dictionary<string, object>
            {
                ["readyPending"] = summary.ReadyPending,
                ["delayedPending"] = summary.DelayedPending,
                ["processing"] = summary.Processing,
                ["deadLetter"] = summary.DeadLetter,
                ["failed"] = summary.Failed,
                ["retryingFailed"] = summary.RetryingFailed,
                ["expiredProcessing"] = summary.ExpiredProcessing,
                ["oldestReadyPendingSeconds"] = summary.OldestReadyPendingSeconds
            };

            if (summary.Failed > 0)
            {
                return HealthCheckResult.Unhealthy("Outbox has failed jobs.", data: data);
            }

            if (summary.DeadLetter > 0)
            {
                return HealthCheckResult.Degraded("Outbox has dead-letter jobs.", data: data);
            }

            if (summary.ExpiredProcessing > 0)
            {
                return HealthCheckResult.Degraded("Outbox has expired processing leases.", data: data);
            }

            if (summary.RetryingFailed > 0)
            {
                return HealthCheckResult.Degraded("Outbox has retrying failed jobs.", data: data);
            }

            if (summary.ReadyPending > options.MaxReadyPendingJobs)
            {
                return HealthCheckResult.Degraded("Outbox ready pending backlog exceeds threshold.", data: data);
            }

            if (summary.OldestReadyPendingSeconds > options.MaxReadyPendingAge.TotalSeconds)
            {
                return HealthCheckResult.Degraded("Outbox ready pending backlog age exceeds threshold.", data: data);
            }

            return HealthCheckResult.Healthy("Outbox backlog is observable.", data);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Outbox backlog is unreachable.", exception);
        }
    }

    private async Task<OutboxBacklogSummary> ReadSummaryAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText = """
            SELECT
                count(*) FILTER (WHERE status = 'pending' AND available_at <= now())::bigint,
                count(*) FILTER (WHERE status = 'pending' AND available_at > now())::bigint,
                count(*) FILTER (WHERE status = 'processing')::bigint,
                count(*) FILTER (WHERE status = 'dead_letter')::bigint,
                count(*) FILTER (WHERE status = 'failed')::bigint,
                count(*) FILTER (
                    WHERE status = 'pending'
                        AND (
                            attempts > 0
                            OR last_error IS NOT NULL
                        )
                )::bigint,
                count(*) FILTER (
                    WHERE status = 'processing'
                        AND locked_until IS NOT NULL
                        AND locked_until <= now()
                )::bigint,
                COALESCE(
                    EXTRACT(EPOCH FROM (
                        now() - MIN(available_at) FILTER (
                            WHERE status = 'pending'
                                AND available_at <= now()
                        )
                    )),
                    0
                )::double precision
            FROM outbox_jobs;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Outbox backlog query returned no result.");
        }

        return new OutboxBacklogSummary(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetDouble(7));
    }

    private sealed record OutboxBacklogSummary(
        long ReadyPending,
        long DelayedPending,
        long Processing,
        long DeadLetter,
        long Failed,
        long RetryingFailed,
        long ExpiredProcessing,
        double OldestReadyPendingSeconds);
}
