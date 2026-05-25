using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Idempotency;

internal static class PostgresApiIdempotencyCompleter
{
    public static async Task CompleteAsync(
        NpgsqlDataSource dataSource,
        Guid idempotencyRecordId,
        string requestHash,
        int responseStatus,
        string? responseBody,
        string? responseContentType,
        string? resourceType,
        Guid? resourceId,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await CompleteAsync(
            connection,
            transaction: null,
            idempotencyRecordId,
            requestHash,
            responseStatus,
            responseBody,
            responseContentType,
            resourceType,
            resourceId,
            failureMessage,
            cancellationToken);
    }

    public static async Task CompleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid idempotencyRecordId,
        string requestHash,
        int responseStatus,
        string? responseBody,
        string? responseContentType,
        string? resourceType,
        Guid? resourceId,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        const string sql = """
            UPDATE api_idempotency_keys
            SET
                response_status = @response_status,
                response_body = @response_body,
                response_content_type = @response_content_type,
                resource_type = @resource_type,
                resource_id = @resource_id,
                status = 'completed'
            WHERE
                id = @id
                AND request_hash = @request_hash
                AND status = 'processing';
            """;

        await using var command = transaction is null
            ? new NpgsqlCommand(sql, connection)
            : new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", idempotencyRecordId);
        command.Parameters.AddWithValue("request_hash", requestHash);
        command.Parameters.AddWithValue("response_status", responseStatus);
        command.Parameters.Add("response_body", NpgsqlDbType.Jsonb).Value =
            responseBody is null ? DBNull.Value : responseBody;
        command.Parameters.Add("response_content_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(responseContentType) ? DBNull.Value : responseContentType;
        command.Parameters.AddWithValue("resource_type", string.IsNullOrWhiteSpace(resourceType) ? DBNull.Value : resourceType);
        command.Parameters.AddWithValue("resource_id", resourceId.HasValue ? resourceId.Value : DBNull.Value);

        var updatedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (updatedRows != 1)
        {
            throw new InvalidOperationException(failureMessage);
        }
    }
}
