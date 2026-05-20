using Npgsql;

namespace MemorySystem.Infrastructure.Outbox;

public sealed class PostgresOutboxJobStore(string connectionString) : IOutboxJobStore
{
    public async Task<IReadOnlyList<OutboxJob>> LeaseAvailableAsync(
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be greater than zero.");
        }

        var now = DateTimeOffset.UtcNow;
        var lockedUntil = now.Add(leaseDuration);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            WITH lease_candidates AS (
                SELECT id
                FROM outbox_jobs
                WHERE
                    (
                        status = 'pending'
                        AND available_at <= @now
                    )
                    OR (
                        status = 'processing'
                        AND locked_until IS NOT NULL
                        AND locked_until <= @now
                    )
                ORDER BY available_at, created_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT @batch_size
            )
            UPDATE outbox_jobs AS job
            SET
                status = 'processing',
                attempts = job.attempts + 1,
                locked_until = @locked_until,
                locked_by = @worker_id,
                last_error = NULL
            FROM lease_candidates
            WHERE job.id = lease_candidates.id
            RETURNING
                job.id,
                job.job_type,
                job.aggregate_type,
                job.aggregate_id,
                job.idempotency_key,
                job.payload::text,
                job.status,
                job.attempts,
                job.available_at,
                job.locked_until,
                job.locked_by,
                job.last_error,
                job.created_at,
                job.updated_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("locked_until", lockedUntil);
        command.Parameters.AddWithValue("worker_id", workerId);
        command.Parameters.AddWithValue("batch_size", batchSize);

        var jobs = new List<OutboxJob>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            jobs.Add(ReadJob(reader));
        }

        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);

        return jobs;
    }

    public async Task<bool> CompleteAsync(OutboxJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        const string sql = """
            UPDATE outbox_jobs
            SET
                status = 'completed',
                locked_until = NULL,
                locked_by = NULL,
                last_error = NULL
            WHERE
                id = @id
                AND status = 'processing'
                AND locked_by = @locked_by
                AND locked_until = @locked_until;
            """;

        return await ExecuteLeasedUpdateAsync(job, sql, cancellationToken) == 1;
    }

    public async Task<bool> MarkFailedAsync(
        OutboxJob job,
        string error,
        bool deadLetter,
        DateTimeOffset? availableAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("Failure error must be provided.", nameof(error));
        }

        var nextStatus = deadLetter ? "dead_letter" : "pending";
        var nextAvailableAt = availableAt ?? DateTimeOffset.UtcNow;

        const string sql = """
            UPDATE outbox_jobs
            SET
                status = @status,
                available_at = @available_at,
                locked_until = NULL,
                locked_by = NULL,
                last_error = @last_error
            WHERE
                id = @id
                AND status = 'processing'
                AND locked_by = @locked_by
                AND locked_until = @locked_until;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        AddLeaseParameters(command, job);
        command.Parameters.AddWithValue("status", nextStatus);
        command.Parameters.AddWithValue("available_at", nextAvailableAt);
        command.Parameters.AddWithValue("last_error", Truncate(error, 4000));

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private async Task<int> ExecuteLeasedUpdateAsync(
        OutboxJob job,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        AddLeaseParameters(command, job);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddLeaseParameters(NpgsqlCommand command, OutboxJob job)
    {
        if (job.LockedUntil is null || string.IsNullOrWhiteSpace(job.LockedBy))
        {
            throw new InvalidOperationException("The outbox job is not leased.");
        }

        command.Parameters.AddWithValue("id", job.Id);
        command.Parameters.AddWithValue("locked_by", job.LockedBy);
        command.Parameters.AddWithValue("locked_until", job.LockedUntil.Value);
    }

    private static OutboxJob ReadJob(NpgsqlDataReader reader)
    {
        return new OutboxJob(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetInt32(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.GetFieldValue<DateTimeOffset>(12),
            reader.GetFieldValue<DateTimeOffset>(13));
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
