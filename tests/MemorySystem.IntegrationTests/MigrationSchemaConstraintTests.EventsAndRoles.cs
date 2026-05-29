using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
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
}
