using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
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
}
