using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class MigrationSchemaConstraintTests
{
    private const string InitialMigration = "001_initial_memory_schema.sql";

    [Fact]
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
                    status,
                    source_event_id
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
                    'active',
                    @event_id
                );
                """,
                connection);

            command.Parameters.AddWithValue("memory_fact_id", Guid.NewGuid());
            command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
            command.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
            command.Parameters.AddWithValue("project_id", ids.ProjectId);
            command.Parameters.AddWithValue("wrong_org_id", ids.OtherOrgId);
            command.Parameters.AddWithValue("event_id", ids.EventId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
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
            command.Parameters.Add("source_event_id", NpgsqlDbType.Uuid).Value = DBNull.Value;

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal(PostgresErrorCodes.NotNullViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
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
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
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
                    'human_approved',
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
                    'human_approved',
                    @event_id
                );
                """,
                connection);

            orgChunkCommand.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
            orgChunkCommand.Parameters.AddWithValue("lens_id", lensId);
            orgChunkCommand.Parameters.AddWithValue("namespace", $"/org/{ids.OrgId}/role/cto/lens");
            orgChunkCommand.Parameters.AddWithValue("org_id_text", ids.OrgId.ToString());
            orgChunkCommand.Parameters.AddWithValue("event_id", ids.EventId);

            var inserted = await orgChunkCommand.ExecuteNonQueryAsync();

            Assert.Equal(1, inserted);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
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
            var memoryFactId = await InsertGlobalMemoryFactAsync(connection, ids.EventId);
            var lensId = await InsertGlobalRoleMemoryLensAsync(connection, ids.EventId, memoryFactId);

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
                    'human_approved',
                    @event_id
                );
                """,
                connection);

            command.Parameters.AddWithValue("chunk_id", Guid.NewGuid());
            command.Parameters.AddWithValue("lens_id", lensId);
            command.Parameters.AddWithValue("event_id", ids.EventId);

            var inserted = await command.ExecuteNonQueryAsync();

            Assert.Equal(1, inserted);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO events (
                        id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @event_id,
                        'memory_written',
                        '{}'::jsonb,
                        'human_approved'
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
                        source_event_id
                    )
                    VALUES (
                        @memory_fact_id,
                        'global',
                        'global',
                        '/global/facts',
                        'project_decision',
                        'system',
                        'storage',
                        'uses',
                        'postgres',
                        0.900,
                        'active',
                        @event_id
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
                        '/global/other',
                        'global',
                        'global',
                        'PostgreSQL stores long-term memory facts.',
                        'existing-bad-chunk-hash',
                        'human_approved',
                        @event_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("event_id", eventId);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
                    'human_approved',
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

    private static async Task<Guid> InsertProjectMemoryFactAsync(
        NpgsqlConnection connection,
        ProjectFixtureIds ids)
    {
        var memoryFactId = Guid.NewGuid();

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
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
        command.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
        command.Parameters.AddWithValue("project_id", ids.ProjectId);
        command.Parameters.AddWithValue("org_id", ids.OrgId);
        command.Parameters.AddWithValue("event_id", ids.EventId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }

    private static async Task<Guid> InsertOrgMemoryFactAsync(
        NpgsqlConnection connection,
        ProjectFixtureIds ids)
    {
        var memoryFactId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
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
                source_event_id
            )
            VALUES (
                @memory_fact_id,
                'org',
                @org_id_text,
                @namespace,
                @org_id,
                'project_decision',
                'org_shared',
                'data boundaries',
                'are',
                'mandatory',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("org_id_text", ids.OrgId.ToString());
        command.Parameters.AddWithValue("namespace", $"/org/{ids.OrgId}/principles");
        command.Parameters.AddWithValue("org_id", ids.OrgId);
        command.Parameters.AddWithValue("event_id", ids.EventId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }

    private static async Task<Guid> InsertGlobalMemoryFactAsync(
        NpgsqlConnection connection,
        Guid eventId)
    {
        var memoryFactId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
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
                source_event_id
            )
            VALUES (
                @memory_fact_id,
                'global',
                'global',
                '/global/role-principles',
                'project_decision',
                'system',
                'source boundaries',
                'must be',
                'auditable',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("event_id", eventId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }

    private static async Task InsertRoleAssignmentAsync(
        NpgsqlConnection connection,
        Guid principalId,
        string scopeType,
        Guid scopeId)
    {
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
                'cto',
                @scope_type,
                @scope_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("assignment_id", Guid.NewGuid());
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> InsertProjectRoleMemoryLensAsync(
        NpgsqlConnection connection,
        ProjectFixtureIds ids,
        Guid baseMemoryFactId)
    {
        var lensId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
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
                source_event_id
            )
            VALUES (
                @lens_id,
                'cto',
                'project',
                @project_id_text,
                @org_id,
                @project_id,
                @base_memory_fact_id,
                'The CTO view treats PostgreSQL as a risk-reduction decision.',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("lens_id", lensId);
        command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
        command.Parameters.AddWithValue("org_id", ids.OrgId);
        command.Parameters.AddWithValue("project_id", ids.ProjectId);
        command.Parameters.AddWithValue("base_memory_fact_id", baseMemoryFactId);
        command.Parameters.AddWithValue("event_id", ids.EventId);

        await command.ExecuteNonQueryAsync();

        return lensId;
    }

    private static async Task<Guid> InsertOrgRoleMemoryLensAsync(
        NpgsqlConnection connection,
        ProjectFixtureIds ids,
        Guid baseMemoryFactId)
    {
        var lensId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO role_memory_lenses (
                id,
                role_id,
                scope_type,
                scope_id,
                org_id,
                base_memory_fact_id,
                interpretation,
                confidence,
                status,
                source_event_id
            )
            VALUES (
                @lens_id,
                'cto',
                'org',
                @org_id_text,
                @org_id,
                @base_memory_fact_id,
                'The CTO view treats org data boundaries as mandatory.',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("lens_id", lensId);
        command.Parameters.AddWithValue("org_id_text", ids.OrgId.ToString());
        command.Parameters.AddWithValue("org_id", ids.OrgId);
        command.Parameters.AddWithValue("base_memory_fact_id", baseMemoryFactId);
        command.Parameters.AddWithValue("event_id", ids.EventId);

        await command.ExecuteNonQueryAsync();

        return lensId;
    }

    private static async Task<Guid> InsertGlobalRoleMemoryLensAsync(
        NpgsqlConnection connection,
        Guid eventId,
        Guid baseMemoryFactId)
    {
        var lensId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO role_memory_lenses (
                id,
                role_id,
                scope_type,
                scope_id,
                base_memory_fact_id,
                interpretation,
                confidence,
                status,
                source_event_id
            )
            VALUES (
                @lens_id,
                'cto',
                'global',
                'global',
                @base_memory_fact_id,
                'The CTO view treats auditable source boundaries as a shared principle.',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("lens_id", lensId);
        command.Parameters.AddWithValue("base_memory_fact_id", baseMemoryFactId);
        command.Parameters.AddWithValue("event_id", eventId);

        await command.ExecuteNonQueryAsync();

        return lensId;
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

    private static async Task<ProjectFixtureIds> InsertProjectFixtureAsync(
        NpgsqlConnection connection,
        bool includeSourceEvent = true)
    {
        var principalId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO principals (id, principal_type, display_name, status)
            VALUES (@principal_id, 'human', 'Test Principal', 'active');

            INSERT INTO organizations (id, name)
            VALUES (@org_id, 'Primary Org'), (@other_org_id, 'Other Org');

            INSERT INTO projects (id, org_id, name, status)
            VALUES (@project_id, @org_id, 'Project', 'active');
            """,
            connection);

        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("other_org_id", otherOrgId);
        command.Parameters.AddWithValue("project_id", projectId);

        await command.ExecuteNonQueryAsync();

        if (includeSourceEvent)
        {
            await using var eventCommand = new NpgsqlCommand(
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
                    @org_id,
                    @project_id,
                    'user_message',
                    '{}'::jsonb,
                    'user_scoped'
                );
                """,
                connection);

            eventCommand.Parameters.AddWithValue("event_id", eventId);
            eventCommand.Parameters.AddWithValue("principal_id", principalId);
            eventCommand.Parameters.AddWithValue("project_id_text", projectId.ToString());
            eventCommand.Parameters.AddWithValue("org_id", orgId);
            eventCommand.Parameters.AddWithValue("project_id", projectId);

            await eventCommand.ExecuteNonQueryAsync();
        }

        return new ProjectFixtureIds(principalId, orgId, otherOrgId, projectId, eventId);
    }

    private sealed record ProjectFixtureIds(
        Guid PrincipalId,
        Guid OrgId,
        Guid OtherOrgId,
        Guid ProjectId,
        Guid EventId);

    private sealed record EventScope(
        string ScopeType,
        string ScopeId,
        string? ScopePrincipalId,
        string? ScopeRoleId);
}
