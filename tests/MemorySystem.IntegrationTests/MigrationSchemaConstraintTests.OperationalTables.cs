using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Outbox_jobs_reject_processing_status_without_lease_metadata()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_outbox_processing_lease_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(
                """
                INSERT INTO outbox_jobs (
                    id,
                    job_type,
                    aggregate_type,
                    aggregate_id,
                    idempotency_key,
                    payload,
                    status
                )
                VALUES (
                    @id,
                    'memory.index',
                    'memory_fact',
                    @aggregate_id,
                    @idempotency_key,
                    '{}',
                    'processing'
                );
                """,
                connection);

            var jobId = Guid.NewGuid();
            command.Parameters.AddWithValue("id", jobId);
            command.Parameters.AddWithValue("aggregate_id", Guid.NewGuid());
            command.Parameters.AddWithValue("idempotency_key", $"outbox-processing-lease:{jobId:N}");

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_embeddings_reject_dimension_that_disagrees_with_vector()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_embedding_dimension_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            var memoryFactId = await InsertProjectMemoryFactAsync(connection, ids);
            var chunkId = Guid.NewGuid();

            await using var chunkCommand = new NpgsqlCommand(
                """
                INSERT INTO memory_chunks (
                    id,
                    source_type,
                    source_id,
                    namespace,
                    scope_type,
                    scope_id,
                    content,
                    content_hash,
                    trust_level,
                    source_event_id
                )
                VALUES (
                    @chunk_id,
                    'memory_fact',
                    @memory_fact_id,
                    @namespace,
                    'project',
                    @project_id_text,
                    'Long-Term Memory System uses PostgreSQL.',
                    'embedding-dimension-test-hash',
                    'user_scoped',
                    @event_id
                );
                """,
                connection);

            chunkCommand.Parameters.AddWithValue("chunk_id", chunkId);
            chunkCommand.Parameters.AddWithValue("memory_fact_id", memoryFactId);
            chunkCommand.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
            chunkCommand.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            chunkCommand.Parameters.AddWithValue("event_id", ids.EventId);

            await chunkCommand.ExecuteNonQueryAsync();

            await using var embeddingCommand = new NpgsqlCommand(
                """
                INSERT INTO memory_embeddings (
                    chunk_id,
                    embedding_model,
                    embedding_dimension,
                    embedding
                )
                VALUES (
                    @chunk_id,
                    'test-embedding-model',
                    2,
                    '[1,2,3]'::vector
                );
                """,
                connection);

            embeddingCommand.Parameters.AddWithValue("chunk_id", chunkId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => embeddingCommand.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_required_role_id_extracts_role_namespaces_from_shared_database_function()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_role_requirement_function_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var projectId = Guid.Parse("33333333-3333-4333-8333-333333333333");
            await using var command = new NpgsqlCommand(
                """
                SELECT
                    memory_required_role_id('/project/' || @project_id || '/role/designer/lens', 'project', @project_id, NULL),
                    memory_required_role_id('/role/developer/shared', 'role', 'developer', NULL),
                    memory_required_role_id('/project/' || @project_id || '/decisions', 'project', @project_id, NULL),
                    memory_required_role_id('/role/', 'global', 'global', NULL);
                """,
                connection);
            command.Parameters.AddWithValue("project_id", projectId.ToString());

            await using var reader = await command.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal("designer", reader.GetString(0));
            Assert.Equal("developer", reader.GetString(1));
            Assert.True(reader.IsDBNull(2));
            Assert.True(reader.IsDBNull(3));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
