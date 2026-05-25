using Npgsql;

namespace MemorySystem.Infrastructure.Workers;

public sealed class PostgresWorkerHeartbeatStore(NpgsqlDataSource dataSource) : IWorkerHeartbeatStore
{
    private const int CommandTimeoutSeconds = 3;
    private const int MaxErrorLength = 4000;

    public async Task RecordAsync(
        WorkerHeartbeatUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrWhiteSpace(update.WorkerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(update.WorkerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(update.Status);

        var now = DateTimeOffset.UtcNow;
        var lastSuccessAt = string.Equals(update.Status, WorkerHeartbeatStatuses.Running, StringComparison.Ordinal)
            ? now
            : (DateTimeOffset?)null;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO worker_heartbeats (
                worker_type,
                worker_id,
                status,
                last_seen_at,
                last_success_at,
                last_error
            )
            VALUES (
                @worker_type,
                @worker_id,
                @status,
                @last_seen_at,
                @last_success_at,
                @last_error
            )
            ON CONFLICT (worker_type, worker_id)
            DO UPDATE SET
                status = EXCLUDED.status,
                last_seen_at = EXCLUDED.last_seen_at,
                last_success_at = COALESCE(EXCLUDED.last_success_at, worker_heartbeats.last_success_at),
                last_error = EXCLUDED.last_error,
                updated_at = now();
            """,
            connection);
        command.CommandTimeout = CommandTimeoutSeconds;
        command.Parameters.AddWithValue("worker_type", update.WorkerType);
        command.Parameters.AddWithValue("worker_id", update.WorkerId);
        command.Parameters.AddWithValue("status", update.Status);
        command.Parameters.AddWithValue("last_seen_at", now);
        command.Parameters.AddWithValue("last_success_at", lastSuccessAt.HasValue ? lastSuccessAt.Value : DBNull.Value);
        command.Parameters.AddWithValue("last_error", string.IsNullOrWhiteSpace(update.LastError)
            ? DBNull.Value
            : Truncate(update.LastError, MaxErrorLength));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<WorkerHeartbeatSnapshot?> ReadLatestAsync(
        string workerType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerType);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
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
            """,
            connection);
        command.CommandTimeout = CommandTimeoutSeconds;
        command.Parameters.AddWithValue("worker_type", workerType);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new WorkerHeartbeatSnapshot(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
