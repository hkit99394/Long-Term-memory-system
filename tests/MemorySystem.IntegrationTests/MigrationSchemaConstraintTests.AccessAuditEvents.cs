using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Access_audit_events_reject_unsupported_action_type()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_access_audit_action_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertAccessAuditEventAsync(
                connection,
                actionType: "memory_payload_export",
                metadataJson: "{}"));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Access_audit_events_reject_payload_metadata_fields()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_access_audit_payload_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertAccessAuditEventAsync(
                connection,
                actionType: "audit_export",
                metadataJson: """{"rawPayload":"secret memory text"}"""));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task InsertAccessAuditEventAsync(
        NpgsqlConnection connection,
        string actionType,
        string metadataJson)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO access_audit_events (
                id,
                action_type,
                outcome,
                audit_metadata
            )
            VALUES (
                @id,
                @action_type,
                'succeeded',
                @audit_metadata
            );
            """,
            connection);

        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("action_type", actionType);
        command.Parameters.Add("audit_metadata", NpgsqlDbType.Jsonb).Value = metadataJson;

        await command.ExecuteNonQueryAsync();
    }
}
