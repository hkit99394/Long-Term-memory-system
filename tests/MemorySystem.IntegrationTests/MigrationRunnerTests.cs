using MemorySystem.Infrastructure.Migrations;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class MigrationRunnerTests
{
    private const long AdvisoryLockKey = 7_404_808_312_433_927_019;
    private const string InitialMigration = "001_initial_memory_schema.sql";
    private const string ScopeHardeningMigration = "002_scope_constraints_and_outbox_hardening.sql";
    private const string MemoryFactScopeConsistencyMigration = "003_memory_fact_scope_consistency.sql";

    [Fact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_applies_migrations_repeatably_when_database_connection_is_configured()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var migrationsDirectory = MigrationTestPaths.FindMigrationsDirectory();
        var databaseName = $"memorysystem_migration_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var firstRun = await SqlMigrationRunner.ApplyAsync(databaseConnectionString, migrationsDirectory);
            var secondRun = await SqlMigrationRunner.ApplyAsync(databaseConnectionString, migrationsDirectory);

            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == InitialMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == ScopeHardeningMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == MemoryFactScopeConsistencyMigration);
            Assert.Empty(secondRun.AppliedMigrations);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == InitialMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == ScopeHardeningMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == MemoryFactScopeConsistencyMigration);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_rejects_empty_migrations_directory_for_initial_database()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_empty_migrations_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);
        var emptyMigrationsDirectory = Directory.CreateTempSubdirectory("memorysystem-empty-migrations-").FullName;

        try
        {
            var exception = await Assert.ThrowsAsync<SqlMigrationFilesNotFoundException>(
                () => SqlMigrationRunner.ApplyAsync(databaseConnectionString, emptyMigrationsDirectory));

            Assert.Equal(emptyMigrationsDirectory, exception.MigrationsDirectory);
            Assert.Contains(".sql migration files", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(emptyMigrationsDirectory, recursive: true);
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_rejects_recorded_migration_missing_from_current_directory()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var migrationsDirectory = MigrationTestPaths.FindMigrationsDirectory();
        var databaseName = $"memorysystem_missing_migration_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);
        var emptyMigrationsDirectory = Directory.CreateTempSubdirectory("memorysystem-empty-migrations-").FullName;

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, migrationsDirectory);

            var exception = await Assert.ThrowsAsync<SqlMigrationMissingException>(
                () => SqlMigrationRunner.ApplyAsync(databaseConnectionString, emptyMigrationsDirectory));

            Assert.Contains(InitialMigration, exception.MigrationNames);
            Assert.Contains(ScopeHardeningMigration, exception.MigrationNames);
            Assert.Contains(MemoryFactScopeConsistencyMigration, exception.MigrationNames);
            Assert.Contains("schema_migrations", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(emptyMigrationsDirectory, recursive: true);
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_times_out_when_advisory_lock_is_held()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var migrationsDirectory = MigrationTestPaths.FindMigrationsDirectory();
        var databaseName = $"memorysystem_lock_timeout_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await using var lockConnection = new NpgsqlConnection(databaseConnectionString);
            await lockConnection.OpenAsync();

            await using var lockCommand = new NpgsqlCommand("SELECT pg_advisory_lock(@lock_key);", lockConnection);
            lockCommand.Parameters.AddWithValue("lock_key", AdvisoryLockKey);
            await lockCommand.ExecuteNonQueryAsync();

            var options = new SqlMigrationRunnerOptions
            {
                AdvisoryLockTimeout = TimeSpan.FromMilliseconds(200),
                AdvisoryLockRetryDelay = TimeSpan.FromMilliseconds(25)
            };

            var exception = await Assert.ThrowsAsync<SqlMigrationAdvisoryLockTimeoutException>(
                () => SqlMigrationRunner.ApplyAsync(databaseConnectionString, migrationsDirectory, options));

            Assert.Equal(options.AdvisoryLockTimeout, exception.Timeout);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
