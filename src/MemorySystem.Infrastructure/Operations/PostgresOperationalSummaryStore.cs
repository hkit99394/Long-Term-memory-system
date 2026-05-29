using MemorySystem.Application.Operations;
using MemorySystem.Infrastructure.Health;
using MemorySystem.Infrastructure.Workers;
using Npgsql;

namespace MemorySystem.Infrastructure.Operations;

public sealed class PostgresOperationalSummaryStore(
    NpgsqlDataSource dataSource,
    WorkerHeartbeatHealthOptions workerOptions) : IOperationalSummaryStore
{
    private const int CommandTimeoutSeconds = 3;

    public async Task<OperationalSummary> ReadAsync(CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
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

            SELECT
                worker_type,
                worker_id,
                status,
                last_seen_at,
                last_success_at,
                last_error
            FROM worker_heartbeats
            WHERE worker_type = @worker_type
            ORDER BY last_seen_at DESC, worker_id
            LIMIT 1;

            SELECT count(*)::bigint
            FROM memory_reviews
            WHERE review_status = 'pending';

            SELECT count(*)::bigint
            FROM vault_exports
            WHERE export_type = 'obsidian_markdown'
                AND status = 'stale';
            """,
            connection);
        command.CommandTimeout = CommandTimeoutSeconds;
        command.Parameters.AddWithValue("worker_type", workerOptions.WorkerType);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var outbox = await ReadOutboxSummaryAsync(reader, cancellationToken);
        await reader.NextResultAsync(cancellationToken);

        var worker = await ReadWorkerSummaryAsync(reader, generatedAt, cancellationToken);
        await reader.NextResultAsync(cancellationToken);

        var reviews = new OperationalReviewSummary(await ReadSingleCountAsync(reader, cancellationToken));
        await reader.NextResultAsync(cancellationToken);

        var vaultExports = new OperationalVaultExportSummary(await ReadSingleCountAsync(reader, cancellationToken));
        var status = DetermineStatus(worker, outbox, reviews, vaultExports);

        return new OperationalSummary(
            generatedAt,
            status,
            new OperationalApiSummary("reachable"),
            worker,
            outbox,
            reviews,
            vaultExports);
    }

    private static async Task<OperationalOutboxSummary> ReadOutboxSummaryAsync(
        NpgsqlDataReader reader,
        CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Operational outbox summary query returned no result.");
        }

        return new OperationalOutboxSummary(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetDouble(7));
    }

    private async Task<OperationalWorkerSummary> ReadWorkerSummaryAsync(
        NpgsqlDataReader reader,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new OperationalWorkerSummary(
                workerOptions.WorkerType,
                Observed: false,
                Status: "missing",
                WorkerId: null,
                LastSeenAt: null,
                LastSeenAgeSeconds: null,
                LastSuccessAt: null,
                LastError: null,
                Stale: true);
        }

        var lastSeenAt = reader.GetFieldValue<DateTimeOffset>(3);
        var age = generatedAt - lastSeenAt;
        var stale = age > workerOptions.MaxAge;

        return new OperationalWorkerSummary(
            reader.GetString(0),
            Observed: true,
            reader.GetString(2),
            reader.GetString(1),
            lastSeenAt,
            Math.Max(0, age.TotalSeconds),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            stale);
    }

    private static async Task<long> ReadSingleCountAsync(
        NpgsqlDataReader reader,
        CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Operational count query returned no result.");
        }

        return reader.GetInt64(0);
    }

    private static string DetermineStatus(
        OperationalWorkerSummary worker,
        OperationalOutboxSummary outbox,
        OperationalReviewSummary reviews,
        OperationalVaultExportSummary vaultExports)
    {
        if (outbox.Failed > 0)
        {
            return "unhealthy";
        }

        if (worker.Stale
            || string.Equals(worker.Status, WorkerHeartbeatStatuses.Error, StringComparison.Ordinal)
            || string.Equals(worker.Status, WorkerHeartbeatStatuses.Stopped, StringComparison.Ordinal)
            || outbox.DeadLetter > 0
            || outbox.RetryingFailed > 0
            || outbox.ExpiredProcessing > 0)
        {
            return "degraded";
        }

        if (reviews.Pending > 0
            || vaultExports.Stale > 0
            || outbox.ReadyPending > 0
            || outbox.DelayedPending > 0
            || outbox.Processing > 0)
        {
            return "needs_attention";
        }

        return "healthy";
    }
}
