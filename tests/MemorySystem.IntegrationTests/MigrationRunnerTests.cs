using MemorySystem.Infrastructure.Migrations;

namespace MemorySystem.IntegrationTests;

public sealed class MigrationRunnerTests
{
    [Fact]
    public async Task ApplyAsync_applies_migrations_repeatably_when_database_connection_is_configured()
    {
        var adminConnectionString = PostgresTestDatabase.AdminConnectionString;

        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            return;
        }

        var migrationsDirectory = FindMigrationsDirectory();
        var databaseName = $"memorysystem_migration_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

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
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
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
}
