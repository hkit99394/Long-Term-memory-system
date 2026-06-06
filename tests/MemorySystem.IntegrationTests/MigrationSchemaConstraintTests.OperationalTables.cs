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

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Expanded_operating_role_ids_are_accepted_by_role_constrained_tables()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_expanded_roles_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            var projectFactId = await InsertProjectMemoryFactAsync(connection, ids);
            var roleEventId = Guid.NewGuid();
            var roleFactId = Guid.NewGuid();
            var lensId = Guid.NewGuid();
            var feedbackId = Guid.NewGuid();
            var packetId = Guid.NewGuid();
            var auditId = Guid.NewGuid();

            await using var command = new NpgsqlCommand(
                """
                INSERT INTO role_assignments (
                    id,
                    principal_id,
                    role_id,
                    scope_type,
                    scope_id
                )
                VALUES (
                    @assignment_id,
                    @principal_id,
                    'product_owner',
                    'project',
                    @project_id
                );

                INSERT INTO memory_access_grants (
                    id,
                    role_id,
                    namespace_prefix,
                    permission
                )
                VALUES (
                    @grant_id,
                    'product_owner',
                    @product_owner_namespace,
                    'read'
                );

                INSERT INTO events (
                    id,
                    principal_id,
                    role_id,
                    scope_type,
                    scope_id,
                    scope_role_id,
                    event_type,
                    content,
                    trust_level
                )
                VALUES (
                    @role_event_id,
                    @principal_id,
                    'security_professional',
                    'role',
                    'security_professional',
                    'security_professional',
                    'user_message',
                    '{}'::jsonb,
                    'user_scoped'
                );

                INSERT INTO memory_facts (
                    id,
                    scope_type,
                    scope_id,
                    namespace,
                    role_id,
                    memory_type,
                    visibility,
                    subject,
                    predicate,
                    object,
                    confidence,
                    trust_level,
                    status,
                    source_event_id,
                    proposed_by_principal_id
                )
                VALUES (
                    @role_fact_id,
                    'role',
                    'security_professional',
                    '/role/security_professional/shared',
                    'security_professional',
                    'fact',
                    'role_shared',
                    'access boundary review',
                    'belongs_to',
                    'security professional',
                    0.900,
                    'user_scoped',
                    'active',
                    @role_event_id,
                    @principal_id
                );

                INSERT INTO role_memory_lenses (
                    id,
                    role_id,
                    scope_type,
                    scope_id,
                    org_id,
                    project_id,
                    base_memory_fact_id,
                    interpretation,
                    confidence,
                    status,
                    source_event_id,
                    proposed_by_principal_id
                )
                VALUES (
                    @lens_id,
                    'release_manager',
                    'project',
                    @project_id_text,
                    @org_id,
                    @project_id,
                    @project_fact_id,
                    'Release manager keeps version evidence tied to rollback readiness.',
                    0.900,
                    'active',
                    @project_event_id,
                    @principal_id
                );

                INSERT INTO memory_retrieval_feedback (
                    id,
                    principal_id,
                    retrieval_mode,
                    query_hash,
                    role_id,
                    feedback_type
                )
                VALUES (
                    @feedback_id,
                    @principal_id,
                    'context_packet',
                    'sha256:expanded-operating-role-feedback',
                    'tester_qa',
                    'useful'
                );

                INSERT INTO memory_context_packets (
                    id,
                    principal_id,
                    query_hash,
                    role_id,
                    item_count
                )
                VALUES (
                    @packet_id,
                    @principal_id,
                    'sha256:expanded-operating-role-packet',
                    'knowledge_steward',
                    0
                );

                INSERT INTO access_audit_events (
                    id,
                    action_type,
                    outcome,
                    actor_principal_id,
                    role_id
                )
                VALUES (
                    @audit_id,
                    'role_assignment_change',
                    'succeeded',
                    @principal_id,
                    'it_manager'
                );
                """,
                connection);

            command.Parameters.AddWithValue("assignment_id", Guid.NewGuid());
            command.Parameters.AddWithValue("principal_id", ids.PrincipalId);
            command.Parameters.AddWithValue("project_id", ids.ProjectId);
            command.Parameters.AddWithValue("grant_id", Guid.NewGuid());
            command.Parameters.AddWithValue("product_owner_namespace", $"/project/{ids.ProjectId}/role/product_owner/lens");
            command.Parameters.AddWithValue("role_event_id", roleEventId);
            command.Parameters.AddWithValue("role_fact_id", roleFactId);
            command.Parameters.AddWithValue("lens_id", lensId);
            command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            command.Parameters.AddWithValue("org_id", ids.OrgId);
            command.Parameters.AddWithValue("project_fact_id", projectFactId);
            command.Parameters.AddWithValue("project_event_id", ids.EventId);
            command.Parameters.AddWithValue("feedback_id", feedbackId);
            command.Parameters.AddWithValue("packet_id", packetId);
            command.Parameters.AddWithValue("audit_id", auditId);

            var inserted = await command.ExecuteNonQueryAsync();

            Assert.Equal(8, inserted);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
