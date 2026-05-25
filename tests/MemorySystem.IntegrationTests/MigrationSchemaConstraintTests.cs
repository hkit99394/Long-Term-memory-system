using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
    private const string InitialMigration = "001_initial_memory_schema.sql";
    private static readonly Guid LegacySystemProvenancePrincipalId = Guid.Parse("00000000-0000-4000-8000-000000000007");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_facts_reject_project_scope_with_mismatched_organization()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_schema_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);

            await using var command = new NpgsqlCommand(
                """
                INSERT INTO memory_facts (
                    id,
                    scope_type,
                    scope_id,
                    namespace,
                    project_id,
                    org_id,
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
                    @memory_fact_id,
                    'project',
                    @project_id_text,
                    @namespace,
                    @project_id,
                    @wrong_org_id,
                    'project_decision',
                    'project_shared',
                    'storage',
                    'uses',
                    'postgres',
                    0.900,
                    'user_scoped',
                    'active',
                    @event_id,
                    @principal_id
                );
                """,
                connection);

            command.Parameters.AddWithValue("memory_fact_id", Guid.NewGuid());
            command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            command.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
            command.Parameters.AddWithValue("project_id", ids.ProjectId);
            command.Parameters.AddWithValue("wrong_org_id", ids.OtherOrgId);
            command.Parameters.AddWithValue("event_id", ids.EventId);
            command.Parameters.AddWithValue("principal_id", ids.PrincipalId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_facts_reject_missing_source_event_provenance()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_fact_provenance_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);

            await using var command = new NpgsqlCommand(
                """
                INSERT INTO memory_facts (
                    id,
                    scope_type,
                    scope_id,
                    namespace,
                    project_id,
                    org_id,
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
                    @memory_fact_id,
                    'project',
                    @project_id_text,
                    @namespace,
                    @project_id,
                    @org_id,
                    'project_decision',
                    'project_shared',
                    'storage',
                    'uses',
                    'postgres',
                    0.900,
                    'user_scoped',
                    'active',
                    @source_event_id,
                    @principal_id
                );
                """,
                connection);

            command.Parameters.AddWithValue("memory_fact_id", Guid.NewGuid());
            command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            command.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
            command.Parameters.AddWithValue("project_id", ids.ProjectId);
            command.Parameters.AddWithValue("org_id", ids.OrgId);
            command.Parameters.Add("source_event_id", NpgsqlDbType.Uuid).Value = DBNull.Value;
            command.Parameters.AddWithValue("principal_id", ids.PrincipalId);

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
    public async Task Memory_facts_reject_missing_proposer_provenance()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_fact_proposer_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);

            await using var command = new NpgsqlCommand(
                """
                INSERT INTO memory_facts (
                    id,
                    scope_type,
                    scope_id,
                    namespace,
                    project_id,
                    org_id,
                    memory_type,
                    visibility,
                    subject,
                    predicate,
                    object,
                    confidence,
                    trust_level,
                    status,
                    source_event_id
                )
                VALUES (
                    @memory_fact_id,
                    'project',
                    @project_id_text,
                    @namespace,
                    @project_id,
                    @org_id,
                    'project_decision',
                    'project_shared',
                    'storage',
                    'uses',
                    'postgres',
                    0.900,
                    'user_scoped',
                    'active',
                    @source_event_id
                );
                """,
                connection);

            command.Parameters.AddWithValue("memory_fact_id", Guid.NewGuid());
            command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            command.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
            command.Parameters.AddWithValue("project_id", ids.ProjectId);
            command.Parameters.AddWithValue("org_id", ids.OrgId);
            command.Parameters.AddWithValue("source_event_id", ids.EventId);

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
    public async Task Memory_facts_reject_source_event_evidence_that_disagrees_with_fact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_fact_source_evidence_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            var otherPrincipalId = Guid.NewGuid();

            await using (var principalCommand = new NpgsqlCommand(
                """
                INSERT INTO principals (id, principal_type, display_name, status)
                VALUES (@principal_id, 'human', 'Other Fact Principal', 'active');
                """,
                connection))
            {
                principalCommand.Parameters.AddWithValue("principal_id", otherPrincipalId);
                await principalCommand.ExecuteNonQueryAsync();
            }

            var otherPrincipalEventId = await InsertScopedEventAsync(
                connection,
                otherPrincipalId,
                "project",
                ids.ProjectId.ToString(),
                scopeOrgId: ids.OrgId,
                scopeProjectId: ids.ProjectId);
            var orgEventId = await InsertScopedEventAsync(
                connection,
                ids.PrincipalId,
                "org",
                ids.OrgId.ToString(),
                scopeOrgId: ids.OrgId);

            await AssertRejectedAsync(ids.EventId, ids.PrincipalId, "human_approved", "trust_level", "trust mismatch");
            await AssertRejectedAsync(otherPrincipalEventId, ids.PrincipalId, "user_scoped", "proposed_by_principal_id", "principal mismatch");
            await AssertRejectedAsync(orgEventId, ids.PrincipalId, "user_scoped", "scope", "scope mismatch");

            async Task AssertRejectedAsync(
                Guid sourceEventId,
                Guid proposedByPrincipalId,
                string trustLevel,
                string expectedMessageFragment,
                string subject)
            {
                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        project_id,
                        org_id,
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
                        @memory_fact_id,
                        'project',
                        @project_id_text,
                        @namespace,
                        @project_id,
                        @org_id,
                        'project_decision',
                        'project_shared',
                        @subject,
                        'uses',
                        'postgres',
                        0.900,
                        @trust_level,
                        'active',
                        @source_event_id,
                        @proposed_by_principal_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("memory_fact_id", Guid.NewGuid());
                command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
                command.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
                command.Parameters.AddWithValue("project_id", ids.ProjectId);
                command.Parameters.AddWithValue("org_id", ids.OrgId);
                command.Parameters.AddWithValue("subject", subject);
                command.Parameters.AddWithValue("trust_level", trustLevel);
                command.Parameters.AddWithValue("source_event_id", sourceEventId);
                command.Parameters.AddWithValue("proposed_by_principal_id", proposedByPrincipalId);

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
    public async Task Events_reject_updates_that_would_invalidate_memory_fact_provenance()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_event_fact_reference_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            await InsertProjectMemoryFactAsync(connection, ids);

            await using var command = new NpgsqlCommand(
                """
                UPDATE events
                SET redaction_status = 'redacted'
                WHERE id = @event_id;
                """,
                connection);
            command.Parameters.AddWithValue("event_id", ids.EventId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("memory_facts", exception.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Events_reject_updates_that_would_invalidate_role_lens_provenance()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_event_lens_reference_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            var memoryFactId = await InsertProjectMemoryFactAsync(connection, ids);
            var lensEventId = await InsertScopedEventAsync(
                connection,
                ids.PrincipalId,
                "project",
                ids.ProjectId.ToString(),
                scopeOrgId: ids.OrgId,
                scopeProjectId: ids.ProjectId);
            await InsertProjectRoleMemoryLensAsync(connection, ids, memoryFactId, lensEventId);

            await using var command = new NpgsqlCommand(
                """
                UPDATE events
                SET retention_class = 'erasure_requested'
                WHERE id = @event_id;
                """,
                connection);
            command.Parameters.AddWithValue("event_id", lensEventId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("role_memory_lenses", exception.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_facts_reject_active_subject_predicate_duplicates_after_trimming()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_fact_trimmed_duplicate_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection);
            await InsertProjectMemoryFactAsync(connection, ids);

            await using var command = new NpgsqlCommand(
                """
                INSERT INTO memory_facts (
                    id,
                    scope_type,
                    scope_id,
                    namespace,
                    project_id,
                    org_id,
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
                    @memory_fact_id,
                    'project',
                    @project_id_text,
                    @namespace,
                    @project_id,
                    @org_id,
                    'project_decision',
                    'project_shared',
                    ' storage ',
                    ' uses ',
                    'mysql',
                    0.900,
                    'user_scoped',
                    'active',
                    @event_id,
                    @principal_id
                );
                """,
                connection);

            command.Parameters.AddWithValue("memory_fact_id", Guid.NewGuid());
            command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            command.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
            command.Parameters.AddWithValue("project_id", ids.ProjectId);
            command.Parameters.AddWithValue("org_id", ids.OrgId);
            command.Parameters.AddWithValue("event_id", ids.EventId);
            command.Parameters.AddWithValue("principal_id", ids.PrincipalId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_access_grants_reject_ambiguous_principal_and_role_target()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_grant_target_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var principalId = Guid.NewGuid();

            await using var principalCommand = new NpgsqlCommand(
                """
                INSERT INTO principals (id, principal_type, display_name, status)
                VALUES (@principal_id, 'human', 'Grant Test Principal', 'active');
                """,
                connection);

            principalCommand.Parameters.AddWithValue("principal_id", principalId);
            await principalCommand.ExecuteNonQueryAsync();

            await using var grantCommand = new NpgsqlCommand(
                """
                INSERT INTO memory_access_grants (
                    id,
                    principal_id,
                    role_id,
                    namespace_prefix,
                    permission
                )
                VALUES (
                    @grant_id,
                    @principal_id,
                    'cto',
                    '/role/cto/shared',
                    'read'
                );
                """,
                connection);

            grantCommand.Parameters.AddWithValue("grant_id", Guid.NewGuid());
            grantCommand.Parameters.AddWithValue("principal_id", principalId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => grantCommand.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal("ck_memory_access_grants_exactly_one_target", exception.ConstraintName);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

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

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Events_reject_project_scope_with_mismatched_organization()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_event_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ids = await InsertProjectFixtureAsync(connection, includeSourceEvent: false);

            await using var command = new NpgsqlCommand(
                """
                INSERT INTO events (
                    id,
                    principal_id,
                    scope_type,
                    scope_id,
                    scope_org_id,
                    scope_project_id,
                    event_type,
                    content,
                    trust_level
                )
                VALUES (
                    @event_id,
                    @principal_id,
                    'project',
                    @project_id_text,
                    @wrong_org_id,
                    @project_id,
                    'user_message',
                    '{}'::jsonb,
                    'user_scoped'
                );
                """,
                connection);

            command.Parameters.AddWithValue("event_id", Guid.NewGuid());
            command.Parameters.AddWithValue("principal_id", ids.PrincipalId);
            command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            command.Parameters.AddWithValue("wrong_org_id", ids.OtherOrgId);
            command.Parameters.AddWithValue("project_id", ids.ProjectId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Role_assignments_reject_missing_org_or_project_scope()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_role_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var principalId = Guid.NewGuid();

            await using var principalCommand = new NpgsqlCommand(
                """
                INSERT INTO principals (id, principal_type, display_name, status)
                VALUES (@principal_id, 'human', 'Role Assignment Principal', 'active');
                """,
                connection);

            principalCommand.Parameters.AddWithValue("principal_id", principalId);
            await principalCommand.ExecuteNonQueryAsync();

            var missingOrgException = await Assert.ThrowsAsync<PostgresException>(() =>
                InsertRoleAssignmentAsync(connection, principalId, "org", Guid.NewGuid()));

            var missingProjectException = await Assert.ThrowsAsync<PostgresException>(() =>
                InsertRoleAssignmentAsync(connection, principalId, "project", Guid.NewGuid()));

            Assert.Equal(PostgresErrorCodes.RaiseException, missingOrgException.SqlState);
            Assert.Equal(PostgresErrorCodes.RaiseException, missingProjectException.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_rejects_existing_role_assignment_with_missing_scope_reference()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_existing_role_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                var principalId = Guid.NewGuid();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@principal_id, 'human', 'Existing Role Principal', 'active');

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
                        'cto',
                        'org',
                        @missing_org_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("principal_id", principalId);
                command.Parameters.AddWithValue("assignment_id", Guid.NewGuid());
                command.Parameters.AddWithValue("missing_org_id", Guid.NewGuid());

                await command.ExecuteNonQueryAsync();
            }

            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory()));

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("existing role_assignments", exception.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_rejects_existing_memory_chunk_with_mismatched_source_scope()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_existing_chunk_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                var eventId = Guid.NewGuid();
                var memoryFactId = Guid.NewGuid();
                var principalId = Guid.NewGuid();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@principal_id, 'human', 'Legacy Chunk Principal', 'active');

                    INSERT INTO events (
                        id,
                        principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @event_id,
                        @principal_id,
                        'memory_written',
                        '{}'::jsonb,
                        'human_approved'
                    );

                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        user_principal_id,
                        memory_type,
                        visibility,
                        subject,
                        predicate,
                        object,
                        confidence,
                        status,
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @memory_fact_id,
                        'user',
                        @principal_id_text,
                        @namespace,
                        @principal_id,
                        'project_decision',
                        'private',
                        'storage',
                        'uses',
                        'postgres',
                        0.900,
                        'active',
                        @event_id,
                        @principal_id
                    );

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
                        'user',
                        @principal_id_text,
                        'PostgreSQL stores long-term memory facts.',
                        'existing-bad-chunk-hash',
                        'human_approved',
                        @event_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("event_id", eventId);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("principal_id", principalId);
                command.Parameters.AddWithValue("principal_id_text", principalId.ToString());
                command.Parameters.AddWithValue("namespace", $"/user/{principalId}/facts");
                command.Parameters.AddWithValue("wrong_namespace", $"/user/{principalId}/other");
                command.Parameters.AddWithValue("chunk_id", Guid.NewGuid());

                await command.ExecuteNonQueryAsync();
            }

            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory()));

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("existing memory_chunks", exception.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_rejects_existing_event_with_ambiguous_legacy_scope_signals()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_ambiguous_event_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                var principalId = Guid.NewGuid();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@principal_id, 'human', 'Ambiguous Event Principal', 'active');

                    INSERT INTO events (
                        id,
                        principal_id,
                        conversation_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @event_id,
                        @principal_id,
                        @conversation_id,
                        'user_message',
                        '{}'::jsonb,
                        'user_scoped'
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("principal_id", principalId);
                command.Parameters.AddWithValue("event_id", Guid.NewGuid());
                command.Parameters.AddWithValue("conversation_id", Guid.NewGuid());

                await command.ExecuteNonQueryAsync();
            }

            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory()));

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("ambiguous legacy scope signals", exception.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_backfills_existing_event_scope_from_legacy_columns()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_event_backfill_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            var userPrincipalId = Guid.NewGuid();
            var agentPrincipalId = Guid.NewGuid();
            var userEventId = Guid.NewGuid();
            var agentEventId = Guid.NewGuid();
            var roleEventId = Guid.NewGuid();
            var sessionEventId = Guid.NewGuid();
            var globalEventId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES
                        (@user_principal_id, 'human', 'Event User', 'active'),
                        (@agent_principal_id, 'agent', 'Event Agent', 'active');

                    INSERT INTO events (
                        id,
                        principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @user_event_id,
                        @user_principal_id,
                        'user_message',
                        '{}'::jsonb,
                        'user_scoped'
                    );

                    INSERT INTO events (
                        id,
                        agent_principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @agent_event_id,
                        @agent_principal_id,
                        'assistant_message',
                        '{}'::jsonb,
                        'agent_private'
                    );

                    INSERT INTO events (
                        id,
                        role_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @role_event_id,
                        'cto',
                        'memory_reviewed',
                        '{}'::jsonb,
                        'human_approved'
                    );

                    INSERT INTO events (
                        id,
                        conversation_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @session_event_id,
                        @conversation_id,
                        'user_message',
                        '{}'::jsonb,
                        'user_scoped'
                    );

                    INSERT INTO events (
                        id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @global_event_id,
                        'tool_call',
                        '{}'::jsonb,
                        'system_trusted'
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("user_principal_id", userPrincipalId);
                command.Parameters.AddWithValue("agent_principal_id", agentPrincipalId);
                command.Parameters.AddWithValue("user_event_id", userEventId);
                command.Parameters.AddWithValue("agent_event_id", agentEventId);
                command.Parameters.AddWithValue("role_event_id", roleEventId);
                command.Parameters.AddWithValue("session_event_id", sessionEventId);
                command.Parameters.AddWithValue("global_event_id", globalEventId);
                command.Parameters.AddWithValue("conversation_id", conversationId);

                await command.ExecuteNonQueryAsync();
            }

            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                var userScope = await ReadEventScopeAsync(connection, userEventId);
                var agentScope = await ReadEventScopeAsync(connection, agentEventId);
                var roleScope = await ReadEventScopeAsync(connection, roleEventId);
                var sessionScope = await ReadEventScopeAsync(connection, sessionEventId);
                var globalScope = await ReadEventScopeAsync(connection, globalEventId);

                Assert.Equal(new EventScope("user", userPrincipalId.ToString(), userPrincipalId.ToString(), null), userScope);
                Assert.Equal(new EventScope("agent", agentPrincipalId.ToString(), agentPrincipalId.ToString(), null), agentScope);
                Assert.Equal(new EventScope("role", "cto", null, "cto"), roleScope);
                Assert.Equal(new EventScope("session", conversationId.ToString(), null, null), sessionScope);
                Assert.Equal(new EventScope("global", "global", null, null), globalScope);
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_reconciles_legacy_principal_events_for_non_user_memory_scopes()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_legacy_event_scope_reconcile_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            var principalId = Guid.NewGuid();
            var orgId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var projectFactEventId = Guid.NewGuid();
            var orgFactEventId = Guid.NewGuid();
            var globalFactEventId = Guid.NewGuid();
            var lensEventId = Guid.NewGuid();
            var projectMemoryFactId = Guid.NewGuid();
            var orgMemoryFactId = Guid.NewGuid();
            var globalMemoryFactId = Guid.NewGuid();
            var lensId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@principal_id, 'human', 'Legacy Scope Principal', 'active');

                    INSERT INTO organizations (id, name)
                    VALUES (@org_id, 'Legacy Scope Org');

                    INSERT INTO projects (id, org_id, name, status)
                    VALUES (@project_id, @org_id, 'Legacy Scope Project', 'active');

                    INSERT INTO events (
                        id,
                        principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES
                        (@project_fact_event_id, @principal_id, 'memory_written', '{}'::jsonb, 'user_scoped'),
                        (@org_fact_event_id, @principal_id, 'memory_written', '{}'::jsonb, 'user_scoped'),
                        (@global_fact_event_id, @principal_id, 'memory_written', '{}'::jsonb, 'user_scoped'),
                        (@lens_event_id, @principal_id, 'memory_reviewed', '{}'::jsonb, 'human_approved');

                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        project_id,
                        org_id,
                        memory_type,
                        visibility,
                        subject,
                        predicate,
                        object,
                        confidence,
                        status,
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @project_memory_fact_id,
                        'project',
                        @project_id_text,
                        @project_namespace,
                        @project_id,
                        @org_id,
                        'project_decision',
                        'project_shared',
                        'storage',
                        'uses',
                        'postgres',
                        0.900,
                        'active',
                        @project_fact_event_id,
                        NULL
                    );

                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        org_id,
                        memory_type,
                        visibility,
                        subject,
                        predicate,
                        object,
                        confidence,
                        status,
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @org_memory_fact_id,
                        'org',
                        @org_id_text,
                        @org_namespace,
                        @org_id,
                        'project_decision',
                        'org_shared',
                        'retention',
                        'requires',
                        'auditability',
                        0.900,
                        'active',
                        @org_fact_event_id,
                        @principal_id
                    );

                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        memory_type,
                        visibility,
                        subject,
                        predicate,
                        object,
                        confidence,
                        status,
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @global_memory_fact_id,
                        'global',
                        'global',
                        '/global/facts',
                        'fact',
                        'system',
                        'source boundaries',
                        'are',
                        'auditable',
                        0.900,
                        'active',
                        @global_fact_event_id,
                        @principal_id
                    );

                    INSERT INTO role_memory_lenses (
                        id,
                        role_id,
                        project_id,
                        base_memory_fact_id,
                        interpretation,
                        confidence,
                        status,
                        source_event_id
                    )
                    VALUES (
                        @lens_id,
                        'cto',
                        @project_id,
                        @project_memory_fact_id,
                        'The CTO view treats PostgreSQL as an auditability decision.',
                        0.900,
                        'active',
                        @lens_event_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("principal_id", principalId);
                command.Parameters.AddWithValue("org_id", orgId);
                command.Parameters.AddWithValue("project_id", projectId);
                command.Parameters.AddWithValue("project_id_text", projectId.ToString());
                command.Parameters.AddWithValue("org_id_text", orgId.ToString());
                command.Parameters.AddWithValue("project_namespace", $"/project/{projectId}/decisions");
                command.Parameters.AddWithValue("org_namespace", $"/org/{orgId}/principles");
                command.Parameters.AddWithValue("project_fact_event_id", projectFactEventId);
                command.Parameters.AddWithValue("org_fact_event_id", orgFactEventId);
                command.Parameters.AddWithValue("global_fact_event_id", globalFactEventId);
                command.Parameters.AddWithValue("lens_event_id", lensEventId);
                command.Parameters.AddWithValue("project_memory_fact_id", projectMemoryFactId);
                command.Parameters.AddWithValue("org_memory_fact_id", orgMemoryFactId);
                command.Parameters.AddWithValue("global_memory_fact_id", globalMemoryFactId);
                command.Parameters.AddWithValue("lens_id", lensId);

                await command.ExecuteNonQueryAsync();
            }

            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                Assert.Equal(new EventScope("project", projectId.ToString(), null, null), await ReadEventScopeAsync(connection, projectFactEventId));
                Assert.Equal(new EventScope("org", orgId.ToString(), null, null), await ReadEventScopeAsync(connection, orgFactEventId));
                Assert.Equal(new EventScope("global", "global", null, null), await ReadEventScopeAsync(connection, globalFactEventId));
                Assert.Equal(new EventScope("project", projectId.ToString(), null, null), await ReadEventScopeAsync(connection, lensEventId));

                await using var lensCommand = new NpgsqlCommand(
                    """
                    SELECT proposed_by_principal_id
                    FROM role_memory_lenses
                    WHERE id = @lens_id;
                    """,
                    connection);
                lensCommand.Parameters.AddWithValue("lens_id", lensId);

                Assert.Equal(principalId, await lensCommand.ExecuteScalarAsync());

                await using var memoryFactCommand = new NpgsqlCommand(
                    """
                    SELECT proposed_by_principal_id
                    FROM memory_facts
                    WHERE id = @memory_fact_id;
                    """,
                    connection);
                memoryFactCommand.Parameters.AddWithValue("memory_fact_id", projectMemoryFactId);

                Assert.Equal(principalId, await memoryFactCommand.ExecuteScalarAsync());
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_preserves_legacy_agent_scoped_memory_fact_provenance()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_agent_fact_provenance_upgrade_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            var agentPrincipalId = Guid.NewGuid();
            var agentEventId = Guid.NewGuid();
            var memoryFactId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@agent_principal_id, 'agent', 'Legacy Agent Principal', 'active');

                    INSERT INTO events (
                        id,
                        agent_principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @agent_event_id,
                        @agent_principal_id,
                        'assistant_message',
                        '{}'::jsonb,
                        'agent_private'
                    );

                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        agent_principal_id,
                        memory_type,
                        visibility,
                        subject,
                        predicate,
                        object,
                        confidence,
                        status,
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @memory_fact_id,
                        'agent',
                        @agent_principal_id_text,
                        @namespace,
                        @agent_principal_id,
                        'agent_private',
                        'private',
                        'indexing worker',
                        'prefers',
                        'short leases',
                        0.900,
                        'active',
                        @agent_event_id,
                        @agent_principal_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("agent_principal_id", agentPrincipalId);
                command.Parameters.AddWithValue("agent_principal_id_text", agentPrincipalId.ToString());
                command.Parameters.AddWithValue("agent_event_id", agentEventId);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("namespace", $"/agent/{agentPrincipalId}/private");

                await command.ExecuteNonQueryAsync();
            }

            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    SELECT proposed_by_principal_id, trust_level
                    FROM memory_facts
                    WHERE id = @memory_fact_id;
                    """,
                    connection);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);

                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(agentPrincipalId, reader.GetGuid(0));
                Assert.Equal("agent_private", reader.GetString(1));
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_backfills_actorless_legacy_source_events_to_system_provenance_principal()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_actorless_provenance_upgrade_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            var globalFactEventId = Guid.NewGuid();
            var lensEventId = Guid.NewGuid();
            var memoryFactId = Guid.NewGuid();
            var lensId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO events (
                        id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES
                        (@global_fact_event_id, 'memory_written', '{}'::jsonb, 'system_trusted'),
                        (@lens_event_id, 'memory_reviewed', '{}'::jsonb, 'system_trusted');

                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        memory_type,
                        visibility,
                        subject,
                        predicate,
                        object,
                        confidence,
                        status,
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @memory_fact_id,
                        'global',
                        'global',
                        '/global/facts',
                        'fact',
                        'system',
                        'source boundaries',
                        'are',
                        'auditable',
                        0.900,
                        'active',
                        @global_fact_event_id,
                        NULL
                    );

                    INSERT INTO role_memory_lenses (
                        id,
                        role_id,
                        base_memory_fact_id,
                        interpretation,
                        confidence,
                        status,
                        source_event_id
                    )
                    VALUES (
                        @lens_id,
                        'cto',
                        @memory_fact_id,
                        'The CTO view treats source boundaries as an auditability principle.',
                        0.900,
                        'active',
                        @lens_event_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("global_fact_event_id", globalFactEventId);
                command.Parameters.AddWithValue("lens_event_id", lensEventId);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("lens_id", lensId);

                await command.ExecuteNonQueryAsync();
            }

            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    SELECT
                        fact.proposed_by_principal_id,
                        fact.trust_level,
                        lens.proposed_by_principal_id,
                        principal.principal_type
                    FROM memory_facts AS fact
                    INNER JOIN role_memory_lenses AS lens ON lens.id = @lens_id
                    INNER JOIN principals AS principal ON principal.id = fact.proposed_by_principal_id
                    WHERE fact.id = @memory_fact_id;
                    """,
                    connection);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("lens_id", lensId);

                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(LegacySystemProvenancePrincipalId, reader.GetGuid(0));
                Assert.Equal("system_trusted", reader.GetString(1));
                Assert.Equal(LegacySystemProvenancePrincipalId, reader.GetGuid(2));
                Assert.Equal("service", reader.GetString(3));
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_backfills_memory_fact_trust_level_from_source_event()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_fact_trust_backfill_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            var eventId = Guid.NewGuid();
            var memoryFactId = Guid.NewGuid();
            var principalId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@principal_id, 'human', 'Legacy Trust Principal', 'active');

                    INSERT INTO events (
                        id,
                        principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @event_id,
                        @principal_id,
                        'memory_written',
                        '{}'::jsonb,
                        'web_content'
                    );

                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        user_principal_id,
                        memory_type,
                        visibility,
                        subject,
                        predicate,
                        object,
                        confidence,
                        status,
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @memory_fact_id,
                        'user',
                        @principal_id_text,
                        @namespace,
                        @principal_id,
                        'fact',
                        'private',
                        'source trust',
                        'comes from',
                        'event',
                        0.900,
                        'active',
                        @event_id,
                        @principal_id
                    );
                    """,
                    connection);
                command.Parameters.AddWithValue("event_id", eventId);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("principal_id", principalId);
                command.Parameters.AddWithValue("principal_id_text", principalId.ToString());
                command.Parameters.AddWithValue("namespace", $"/user/{principalId}/facts");

                await command.ExecuteNonQueryAsync();
            }

            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    "SELECT trust_level FROM memory_facts WHERE id = @memory_fact_id;",
                    connection);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);

                Assert.Equal("web_content", await command.ExecuteScalarAsync());
            }
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

    private static async Task<string> CreateMigratedDatabaseAsync(string adminConnectionString, string databaseName)
    {
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);
        await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

        return databaseConnectionString;
    }

    private static async Task ApplyInitialMigrationAsync(string databaseConnectionString)
    {
        var migrationsDirectory = CreateMigrationSubsetDirectory(InitialMigration);

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, migrationsDirectory);
        }
        finally
        {
            Directory.Delete(migrationsDirectory, recursive: true);
        }
    }

    private static string CreateMigrationSubsetDirectory(params string[] migrationNames)
    {
        var sourceDirectory = MigrationTestPaths.FindMigrationsDirectory();
        var targetDirectory = Directory.CreateTempSubdirectory("memorysystem-migration-subset-").FullName;

        foreach (var migrationName in migrationNames)
        {
            File.Copy(
                Path.Combine(sourceDirectory, migrationName),
                Path.Combine(targetDirectory, migrationName));
        }

        return targetDirectory;
    }

    private static async Task<EventScope> ReadEventScopeAsync(NpgsqlConnection connection, Guid eventId)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                scope_type,
                scope_id,
                scope_principal_id,
                scope_role_id
            FROM events
            WHERE id = @event_id;
            """,
            connection);

        command.Parameters.AddWithValue("event_id", eventId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new EventScope(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2).ToString(),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private sealed record EventScope(
        string ScopeType,
        string ScopeId,
        string? ScopePrincipalId,
        string? ScopeRoleId);
}
