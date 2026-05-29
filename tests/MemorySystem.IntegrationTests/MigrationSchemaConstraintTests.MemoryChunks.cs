using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_chunks_reject_scope_or_namespace_that_disagrees_with_source_memory_fact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_chunk_fact_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            var memoryFactId = await InsertProjectMemoryFactAsync(connection, ids);

            await using var command = new NpgsqlCommand(
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
                    @wrong_namespace,
                    'project',
                    @project_id_text,
                    'Long-Term Memory System uses PostgreSQL.',
                    'test-hash',
                    'user_scoped',
                    @event_id
                );
                """,
                connection);

            command.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
            command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
            command.Parameters.AddWithValue("wrong_namespace", $"/project/{ids.ProjectId}/other");
            command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            command.Parameters.AddWithValue("event_id", ids.EventId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_chunks_reject_missing_source_event_provenance()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_chunk_provenance_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            var memoryFactId = await InsertProjectMemoryFactAsync(connection, ids);

            await using var command = new NpgsqlCommand(
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
                    'test-hash',
                    'human_approved',
                    @source_event_id
                );
                """,
                connection);

            command.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
            command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
            command.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
            command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            command.Parameters.Add("source_event_id", NpgsqlDbType.Uuid).Value = DBNull.Value;

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.NotNullViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Events_reject_updates_that_would_invalidate_memory_chunk_provenance()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_event_chunk_reference_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);

            await using (var insertChunk = new NpgsqlCommand(
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
                    'document',
                    @document_id,
                    @namespace,
                    'project',
                    @project_id_text,
                    'Long-Term Memory System uses PostgreSQL.',
                    'test-hash',
                    'user_scoped',
                    @event_id
                );
                """,
                connection))
            {
                insertChunk.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
                insertChunk.Parameters.AddWithValue("document_id", Guid.NewGuid());
                insertChunk.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/documents");
                insertChunk.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
                insertChunk.Parameters.AddWithValue("event_id", ids.EventId);

                await insertChunk.ExecuteNonQueryAsync();
            }

            await using var command = new NpgsqlCommand(
                """
                UPDATE events
                SET trust_level = 'human_approved'
                WHERE id = @event_id;
                """,
                connection);
            command.Parameters.AddWithValue("event_id", ids.EventId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("memory_chunks", exception.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_chunks_reject_source_event_evidence_that_disagrees_with_source_memory_fact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_chunk_fact_evidence_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            var memoryFactId = await InsertProjectMemoryFactAsync(connection, ids);
            var alternateEventId = await InsertScopedEventAsync(
                connection,
                ids.PrincipalId,
                "project",
                ids.ProjectId.ToString(),
                scopeOrgId: ids.OrgId,
                scopeProjectId: ids.ProjectId);

            await AssertRejectedAsync(ids.EventId, "human_approved", "trust_level", "chunk-trust-mismatch");
            await AssertRejectedAsync(alternateEventId, "user_scoped", "source_event_id", "chunk-source-event-mismatch");

            async Task AssertRejectedAsync(
                Guid sourceEventId,
                string trustLevel,
                string expectedMessageFragment,
                string contentHash)
            {
                await using var command = new NpgsqlCommand(
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
                        @content_hash,
                        @trust_level,
                        @source_event_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
                command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
                command.Parameters.AddWithValue("content_hash", contentHash);
                command.Parameters.AddWithValue("trust_level", trustLevel);
                command.Parameters.AddWithValue("source_event_id", sourceEventId);

                var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

                Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
                Assert.Contains(expectedMessageFragment, exception.MessageText, StringComparison.Ordinal);
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_chunks_reject_scope_or_namespace_that_disagrees_with_source_role_memory_lens()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_chunk_lens_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            var memoryFactId = await InsertProjectMemoryFactAsync(connection, ids);
            var lensId = await InsertProjectRoleMemoryLensAsync(connection, ids, memoryFactId);

            await using var command = new NpgsqlCommand(
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
                    'role_memory_lens',
                    @lens_id,
                    @wrong_namespace,
                    'project',
                    @project_id_text,
                    'The CTO view treats PostgreSQL as a risk-reduction decision.',
                    'test-hash',
                    'human_approved',
                    @event_id
                );
                """,
                connection);

            command.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
            command.Parameters.AddWithValue("lens_id", lensId);
            command.Parameters.AddWithValue("wrong_namespace", $"/project/{ids.ProjectId}/role/developer/lens");
            command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            command.Parameters.AddWithValue("event_id", ids.EventId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_chunks_require_org_scope_for_org_role_memory_lenses()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_chunk_org_lens_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            var memoryFactId = await InsertOrgMemoryFactAsync(connection, ids);
            var lensId = await InsertOrgRoleMemoryLensAsync(connection, ids, memoryFactId);
            var lensSourceEventId = await ReadRoleMemoryLensSourceEventIdAsync(connection, lensId);

            await using var globalChunkCommand = new NpgsqlCommand(
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
                    'role_memory_lens',
                    @lens_id,
                    '/role/cto/shared',
                    'role',
                    'cto',
                    'The CTO view treats org data boundaries as mandatory.',
                    'org-lens-global-hash',
                    'human_approved',
                    @event_id
                );
                """,
                connection);

            globalChunkCommand.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
            globalChunkCommand.Parameters.AddWithValue("lens_id", lensId);
            globalChunkCommand.Parameters.AddWithValue("event_id", ids.EventId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => globalChunkCommand.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);

            await using var orgChunkCommand = new NpgsqlCommand(
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
                    'role_memory_lens',
                    @lens_id,
                    @namespace,
                    'org',
                    @org_id_text,
                    'The CTO view treats org data boundaries as mandatory.',
                    'org-lens-org-hash',
                    'user_scoped',
                    @event_id
                );
                """,
                connection);

            orgChunkCommand.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
            orgChunkCommand.Parameters.AddWithValue("lens_id", lensId);
            orgChunkCommand.Parameters.AddWithValue("namespace", $"/org/{ids.OrgId}/role/cto/lens");
            orgChunkCommand.Parameters.AddWithValue("org_id_text", ids.OrgId.ToString());
            orgChunkCommand.Parameters.AddWithValue("event_id", lensSourceEventId);

            var inserted = await orgChunkCommand.ExecuteNonQueryAsync();

            Assert.Equal(1, inserted);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_chunks_allow_global_shared_scope_for_global_role_memory_lenses()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_chunk_global_lens_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            var globalEventId = await InsertScopedEventAsync(
                connection,
                ids.PrincipalId,
                "global",
                "global");
            var memoryFactId = await InsertGlobalMemoryFactAsync(connection, globalEventId, ids.PrincipalId);
            var lensId = await InsertGlobalRoleMemoryLensAsync(connection, globalEventId, ids.PrincipalId, memoryFactId);

            await AssertRejectedAsync(ids.EventId, "user_scoped", "source_event_id", "global-lens-wrong-event");
            await AssertRejectedAsync(globalEventId, "human_approved", "trust_level", "global-lens-wrong-trust");

            await using var command = new NpgsqlCommand(
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
                    'role_memory_lens',
                    @lens_id,
                    '/role/cto/shared',
                    'role',
                    'cto',
                    'The CTO view treats auditable source boundaries as a shared principle.',
                    'global-lens-role-hash',
                    'user_scoped',
                    @event_id
                );
                """,
                connection);

            command.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
            command.Parameters.AddWithValue("lens_id", lensId);
            command.Parameters.AddWithValue("event_id", globalEventId);

            var inserted = await command.ExecuteNonQueryAsync();

            Assert.Equal(1, inserted);

            async Task AssertRejectedAsync(
                Guid sourceEventId,
                string trustLevel,
                string expectedMessageFragment,
                string contentHash)
            {
                await using var rejectedCommand = new NpgsqlCommand(
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
                        'role_memory_lens',
                        @lens_id,
                        '/role/cto/shared',
                        'role',
                        'cto',
                        'The CTO view treats auditable source boundaries as a shared principle.',
                        @content_hash,
                        @trust_level,
                        @event_id
                    );
                    """,
                    connection);

                rejectedCommand.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
                rejectedCommand.Parameters.AddWithValue("lens_id", lensId);
                rejectedCommand.Parameters.AddWithValue("content_hash", contentHash);
                rejectedCommand.Parameters.AddWithValue("trust_level", trustLevel);
                rejectedCommand.Parameters.AddWithValue("event_id", sourceEventId);

                var exception = await Assert.ThrowsAsync<PostgresException>(() => rejectedCommand.ExecuteNonQueryAsync());

                Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
                Assert.Contains(expectedMessageFragment, exception.MessageText, StringComparison.Ordinal);
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
