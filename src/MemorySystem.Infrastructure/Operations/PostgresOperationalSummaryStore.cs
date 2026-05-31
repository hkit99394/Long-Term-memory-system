using MemorySystem.Application.Operations;
using MemorySystem.Infrastructure.Health;
using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Infrastructure.Workers;
using Npgsql;

namespace MemorySystem.Infrastructure.Operations;

public sealed class PostgresOperationalSummaryStore(
    NpgsqlDataSource dataSource,
    WorkerHeartbeatHealthOptions workerOptions) : IOperationalSummaryStore
{
    private const int CommandTimeoutSeconds = 3;
    private static readonly TimeSpan RetrievalFeedbackWindow = TimeSpan.FromHours(24);

    public async Task<OperationalSummary> ReadAsync(CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var retrievalFeedbackWindowStartedAt = generatedAt - RetrievalFeedbackWindow;
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

            SELECT
                count(*) FILTER (
                    WHERE job_type = @memory_index_job_type
                        AND status = 'pending'
                        AND (
                            attempts > 0
                            OR last_error IS NOT NULL
                        )
                )::bigint,
                count(*) FILTER (
                    WHERE job_type = @memory_index_job_type
                        AND status = 'dead_letter'
                )::bigint,
                count(*) FILTER (
                    WHERE job_type = @memory_index_job_type
                        AND status = 'failed'
                )::bigint,
                count(*) FILTER (
                    WHERE job_type = @memory_index_job_type
                        AND status = 'processing'
                        AND locked_until IS NOT NULL
                        AND locked_until <= now()
                )::bigint
            FROM outbox_jobs;

            SELECT
                feedback_types.feedback_type,
                count(feedback.id)::bigint
            FROM (
                VALUES
                    ('useful', 1),
                    ('stale', 2),
                    ('wrong', 3),
                    ('sensitive', 4),
                    ('over_broad', 5),
                    ('missing', 6),
                    ('noisy', 7)
            ) AS feedback_types(feedback_type, sort_order)
            LEFT JOIN memory_retrieval_feedback feedback
                ON feedback.feedback_type = feedback_types.feedback_type
                AND feedback.created_at >= @retrieval_feedback_window_started_at
                AND feedback.created_at < @retrieval_feedback_window_ended_at
            GROUP BY feedback_types.feedback_type, feedback_types.sort_order
            ORDER BY feedback_types.sort_order;
            """,
            connection);
        command.CommandTimeout = CommandTimeoutSeconds;
        command.Parameters.AddWithValue("worker_type", workerOptions.WorkerType);
        command.Parameters.AddWithValue("memory_index_job_type", MemoryIndexOutboxJobContract.JobType);
        command.Parameters.AddWithValue("retrieval_feedback_window_started_at", retrievalFeedbackWindowStartedAt);
        command.Parameters.AddWithValue("retrieval_feedback_window_ended_at", generatedAt);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var outbox = await ReadOutboxSummaryAsync(reader, cancellationToken);
        await reader.NextResultAsync(cancellationToken);

        var worker = await ReadWorkerSummaryAsync(reader, generatedAt, cancellationToken);
        await reader.NextResultAsync(cancellationToken);

        var reviews = new OperationalReviewSummary(await ReadSingleCountAsync(reader, cancellationToken));
        await reader.NextResultAsync(cancellationToken);

        var vaultExports = new OperationalVaultExportSummary(await ReadSingleCountAsync(reader, cancellationToken));
        await reader.NextResultAsync(cancellationToken);

        var embeddingFailures = await ReadEmbeddingFailureSummaryAsync(reader, cancellationToken);
        await reader.NextResultAsync(cancellationToken);

        var retrievalFeedback = await ReadRetrievalFeedbackSummaryAsync(
            reader,
            retrievalFeedbackWindowStartedAt,
            generatedAt,
            cancellationToken);
        var status = DetermineStatus(worker, outbox, reviews, vaultExports);

        return new OperationalSummary(
            generatedAt,
            status,
            new OperationalApiSummary("reachable"),
            worker,
            outbox,
            reviews,
            vaultExports,
            retrievalFeedback,
            embeddingFailures);
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

    private static async Task<OperationalEmbeddingFailureSummary> ReadEmbeddingFailureSummaryAsync(
        NpgsqlDataReader reader,
        CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Operational embedding failure query returned no result.");
        }

        return new OperationalEmbeddingFailureSummary(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3));
    }

    private static async Task<OperationalRetrievalFeedbackSummary> ReadRetrievalFeedbackSummaryAsync(
        NpgsqlDataReader reader,
        DateTimeOffset windowStartedAt,
        DateTimeOffset windowEndedAt,
        CancellationToken cancellationToken)
    {
        var rows = new List<(string FeedbackType, long Count)>();

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((reader.GetString(0), reader.GetInt64(1)));
        }

        var total = rows.Sum(row => row.Count);
        var windowHours = Math.Max((windowEndedAt - windowStartedAt).TotalHours, 1.0 / 60.0);
        var byType = rows
            .Select(row => new OperationalRetrievalFeedbackTypeSummary(
                row.FeedbackType,
                row.Count,
                total == 0 ? 0m : Math.Round((decimal)row.Count / total, 6, MidpointRounding.AwayFromZero),
                row.Count / windowHours))
            .ToArray();

        return new OperationalRetrievalFeedbackSummary(
            windowStartedAt,
            windowEndedAt,
            windowHours,
            total,
            byType);
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
