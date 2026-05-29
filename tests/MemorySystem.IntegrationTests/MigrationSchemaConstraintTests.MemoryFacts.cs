using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
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
}
