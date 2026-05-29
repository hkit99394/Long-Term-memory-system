using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
    private const string InitialMigration = "001_initial_memory_schema.sql";
    private static readonly Guid LegacySystemProvenancePrincipalId = Guid.Parse("00000000-0000-4000-8000-000000000007");

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
