using Npgsql;
namespace MemorySystem.Infrastructure.Idempotency;

public sealed class PostgresApiIdempotencyStore(NpgsqlDataSource dataSource) : IApiIdempotencyStore
{
    private const int ExpiredRecordCleanupLimit = 100;

    public async Task<ApiIdempotencyBeginResult> BeginAsync(
        Guid principalId,
        string endpoint,
        string idempotencyKey,
        string requestHash,
        IReadOnlyCollection<string> acceptedRequestHashes,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentNullException.ThrowIfNull(acceptedRequestHashes);

        var now = DateTimeOffset.UtcNow;
        var recordId = Guid.NewGuid();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await DeleteExpiredRecordAsync(
            connection,
            transaction,
            principalId,
            endpoint,
            idempotencyKey,
            now,
            cancellationToken);
        await DeleteExpiredRecordsAsync(
            connection,
            transaction,
            now,
            ExpiredRecordCleanupLimit,
            cancellationToken);

        var inserted = await InsertProcessingRecordAsync(
            connection,
            transaction,
            recordId,
            principalId,
            endpoint,
            idempotencyKey,
            requestHash,
            expiresAt,
            cancellationToken);

        var record = inserted
            ?? await ReadRecordAsync(
                connection,
                transaction,
                principalId,
                endpoint,
                idempotencyKey,
                cancellationToken)
            ?? throw new InvalidOperationException("Idempotency record was not found after insert conflict.");

        await transaction.CommitAsync(cancellationToken);

        return new ApiIdempotencyBeginResult(
            GetBeginStatus(record, acceptedRequestHashes, inserted is not null),
            record);
    }

    public async Task CompleteAsync(
        Guid recordId,
        string requestHash,
        int responseStatus,
        string? responseBody,
        string? responseContentType,
        string? resourceType,
        Guid? resourceId,
        CancellationToken cancellationToken = default)
    {
        await PostgresApiIdempotencyCompleter.CompleteAsync(
            dataSource,
            recordId,
            requestHash,
            responseStatus,
            responseBody,
            responseContentType,
            resourceType,
            resourceId,
            "The idempotency record could not be completed.",
            cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid recordId,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        const string sql = """
            UPDATE api_idempotency_keys
            SET status = 'failed'
            WHERE
                id = @id
                AND request_hash = @request_hash
                AND status = 'processing';
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("id", recordId);
        command.Parameters.AddWithValue("request_hash", requestHash);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteExpiredRecordAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid principalId,
        string endpoint,
        string idempotencyKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM api_idempotency_keys
            WHERE
                principal_id = @principal_id
                AND endpoint = @endpoint
                AND idempotency_key = @idempotency_key
                AND expires_at <= @now;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddKeyParameters(command, principalId, endpoint, idempotencyKey);
        command.Parameters.AddWithValue("now", now);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteExpiredRecordsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH expired_records AS (
                SELECT id
                FROM api_idempotency_keys
                WHERE status IN ('processing', 'completed', 'failed')
                    AND expires_at <= @now
                ORDER BY expires_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT @limit
            )
            DELETE FROM api_idempotency_keys AS record
            USING expired_records
            WHERE record.id = expired_records.id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("limit", limit);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ApiIdempotencyRecord?> InsertProcessingRecordAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid recordId,
        Guid principalId,
        string endpoint,
        string idempotencyKey,
        string requestHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO api_idempotency_keys (
                id,
                principal_id,
                endpoint,
                idempotency_key,
                request_hash,
                status,
                expires_at
            )
            VALUES (
                @id,
                @principal_id,
                @endpoint,
                @idempotency_key,
                @request_hash,
                'processing',
                @expires_at
            )
            ON CONFLICT (principal_id, endpoint, idempotency_key) DO NOTHING
            RETURNING
                id,
                principal_id,
                endpoint,
                idempotency_key,
                request_hash,
                response_status,
                response_body::text,
                response_content_type,
                resource_type,
                resource_id,
                status,
                expires_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", recordId);
        AddKeyParameters(command, principalId, endpoint, idempotencyKey);
        command.Parameters.AddWithValue("request_hash", requestHash);
        command.Parameters.AddWithValue("expires_at", expiresAt);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadRecord(reader) : null;
    }

    private static async Task<ApiIdempotencyRecord?> ReadRecordAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid principalId,
        string endpoint,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id,
                principal_id,
                endpoint,
                idempotency_key,
                request_hash,
                response_status,
                response_body::text,
                response_content_type,
                resource_type,
                resource_id,
                status,
                expires_at
            FROM api_idempotency_keys
            WHERE
                principal_id = @principal_id
                AND endpoint = @endpoint
                AND idempotency_key = @idempotency_key;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddKeyParameters(command, principalId, endpoint, idempotencyKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadRecord(reader) : null;
    }

    private static ApiIdempotencyBeginStatus GetBeginStatus(
        ApiIdempotencyRecord record,
        IReadOnlyCollection<string> acceptedRequestHashes,
        bool inserted)
    {
        if (inserted)
        {
            return ApiIdempotencyBeginStatus.Started;
        }

        if (!acceptedRequestHashes.Contains(record.RequestHash, StringComparer.Ordinal))
        {
            return ApiIdempotencyBeginStatus.Conflict;
        }

        return record.Status switch
        {
            "completed" when record.ResponseStatus.HasValue => ApiIdempotencyBeginStatus.Replay,
            "failed" when record.ResponseStatus.HasValue => ApiIdempotencyBeginStatus.Replay,
            "processing" => ApiIdempotencyBeginStatus.Processing,
            "failed" => ApiIdempotencyBeginStatus.PreviousFailed,
            _ => ApiIdempotencyBeginStatus.Conflict
        };
    }

    private static void AddKeyParameters(
        NpgsqlCommand command,
        Guid principalId,
        string endpoint,
        string idempotencyKey)
    {
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("endpoint", endpoint);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
    }

    private static ApiIdempotencyRecord ReadRecord(NpgsqlDataReader reader)
    {
        return new ApiIdempotencyRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetGuid(9),
            reader.GetString(10),
            reader.GetFieldValue<DateTimeOffset>(11));
    }
}
