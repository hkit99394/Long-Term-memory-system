using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

internal static class ApiDatabaseTestSupport
{
    public static async Task ApplyMigrationsAsync(string connectionString)
    {
        await SqlMigrationRunner.ApplyAsync(connectionString, MigrationTestPaths.FindMigrationsDirectory());
    }

    public static async Task InsertPrincipalAsync(
        string connectionString,
        Guid principalId,
        string principalType = "human",
        string displayName = "Jack Tam")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO principals (
                id,
                principal_type,
                display_name,
                status
            )
            VALUES (
                @principal_id,
                @principal_type,
                @display_name,
                'active'
            );
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("principal_type", principalType);
        command.Parameters.AddWithValue("display_name", displayName);

        await command.ExecuteNonQueryAsync();
    }

    public static async Task InsertOrganizationAndProjectAsync(
        string connectionString,
        Guid orgId,
        Guid projectId,
        string organizationName = "Memory Lab",
        string projectName = "Long-Term Memory System")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO organizations (id, name)
            VALUES (@org_id, @organization_name);

            INSERT INTO projects (id, org_id, name, status)
            VALUES (@project_id, @org_id, @project_name, 'active');
            """,
            connection);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("organization_name", organizationName);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("project_name", projectName);

        await command.ExecuteNonQueryAsync();
    }

    public static async Task InsertSourceEventAsync(
        string connectionString,
        Guid eventId,
        Guid principalId,
        string scopeType,
        string scopeId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO events (
                id,
                principal_id,
                event_type,
                content,
                content_hash,
                retention_class,
                sensitivity,
                trust_level,
                scope_type,
                scope_id,
                scope_principal_id
            )
            VALUES (
                @event_id,
                @principal_id,
                'user_message',
                @content,
                'sha256:test',
                'standard',
                'none',
                'user_scoped',
                @scope_type,
                @scope_id,
                @scope_principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.Add("content", NpgsqlDbType.Jsonb).Value = """{"message":"source"}""";
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.AddWithValue("scope_principal_id", scopeType is "user" or "agent" ? principalId : DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public static async Task<int> CountRowsAsync(string connectionString, string tableName)
    {
        if (!AllowedCountTables.Contains(tableName))
        {
            throw new ArgumentException($"Table '{tableName}' is not supported by the test row counter.", nameof(tableName));
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {tableName};", connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public static async Task<int> CountIdempotencyRecordsAsync(string connectionString)
    {
        return await CountRowsAsync(connectionString, "api_idempotency_keys");
    }

    public static async Task<ApiIdempotencyRecordSummary> ReadSingleIdempotencySummaryAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT endpoint, idempotency_key, status, response_status, resource_type, resource_id
            FROM api_idempotency_keys;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new ApiIdempotencyRecordSummary(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5));
    }

    public static async Task<IReadOnlyList<ApiIdempotencyRecordDetail>> ReadIdempotencyDetailsAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                principal_id,
                endpoint,
                idempotency_key,
                request_hash,
                response_status,
                response_body::text,
                status,
                expires_at
            FROM api_idempotency_keys
            ORDER BY principal_id;
            """,
            connection);

        var records = new List<ApiIdempotencyRecordDetail>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            records.Add(new ApiIdempotencyRecordDetail(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return records;
    }

    private static readonly IReadOnlySet<string> AllowedCountTables = new HashSet<string>(StringComparer.Ordinal)
    {
        "api_idempotency_keys",
        "events",
        "memory_chunks",
        "memory_facts",
        "outbox_jobs"
    };
}

internal sealed record ApiIdempotencyRecordSummary(
    string Endpoint,
    string IdempotencyKey,
    string Status,
    int ResponseStatus,
    string? ResourceType,
    Guid? ResourceId);

internal sealed record ApiIdempotencyRecordDetail(
    Guid PrincipalId,
    string Endpoint,
    string IdempotencyKey,
    string RequestHash,
    int ResponseStatus,
    string ResponseBody,
    string Status,
    DateTimeOffset ExpiresAt);
