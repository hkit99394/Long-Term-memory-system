using MemorySystem.Infrastructure.Migrations;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class MigrationRunnerTests
{
    [Fact]
    public async Task ApplyAsync_applies_migrations_repeatably_when_database_connection_is_configured()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            return;
        }

        var migrationsDirectory = FindMigrationsDirectory();
        var databaseName = $"memorysystem_migration_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            var firstRun = await SqlMigrationRunner.ApplyAsync(databaseConnectionString, migrationsDirectory);
            var secondRun = await SqlMigrationRunner.ApplyAsync(databaseConnectionString, migrationsDirectory);

            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == "001_initial_memory_schema.sql");
            Assert.Empty(secondRun.AppliedMigrations);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == "001_initial_memory_schema.sql");
        }
        finally
        {
            await DropDatabaseAsync(adminConnectionString, databaseName);
        }
    }

    private static string FindMigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "migrations");

            if (File.Exists(Path.Combine(candidate, "001_initial_memory_schema.sql")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository migrations directory.");
    }

    private static async Task<string> CreateDatabaseAsync(string adminConnectionString, string databaseName)
    {
        var testDatabaseConnectionString = BuildDatabaseConnectionString(adminConnectionString, databaseName);

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand($"CREATE DATABASE {QuoteIdentifier(databaseName)};", connection);
        await command.ExecuteNonQueryAsync();

        return testDatabaseConnectionString;
    }

    private static async Task DropDatabaseAsync(string adminConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS {QuoteIdentifier(databaseName)} WITH (FORCE);",
            connection);

        await command.ExecuteNonQueryAsync();
    }

    private static string BuildDatabaseConnectionString(string connectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName
        };

        return builder.ConnectionString;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
