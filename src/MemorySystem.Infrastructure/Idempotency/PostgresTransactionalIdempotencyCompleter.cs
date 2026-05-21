using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Idempotency;

internal static class PostgresTransactionalIdempotencyCompleter
{
    public static async Task CompleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid idempotencyRecordId,
        string requestHash,
        int responseStatus,
        string? responseBody,
        string resourceType,
        Guid resourceId,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        const string sql = """
            UPDATE api_idempotency_keys
            SET
                response_status = @response_status,
                response_body = @response_body,
                resource_type = @resource_type,
                resource_id = @resource_id,
                status = 'completed'
            WHERE
                id = @id
                AND request_hash = @request_hash
                AND status = 'processing';
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", idempotencyRecordId);
        command.Parameters.AddWithValue("request_hash", requestHash);
        command.Parameters.AddWithValue("response_status", responseStatus);
        command.Parameters.Add("response_body", NpgsqlDbType.Jsonb).Value =
            responseBody is null ? DBNull.Value : responseBody;
        command.Parameters.AddWithValue("resource_type", resourceType);
        command.Parameters.AddWithValue("resource_id", resourceId);

        var updatedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (updatedRows != 1)
        {
            throw new InvalidOperationException(failureMessage);
        }
    }
}
