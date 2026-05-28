using MemorySystem.Infrastructure.Migrations;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class MigrationRunnerTests
{
    private const long AdvisoryLockKey = 7_404_808_312_433_927_019;
    private const string InitialMigration = "001_initial_memory_schema.sql";
    private const string ScopeHardeningMigration = "002_scope_constraints_and_outbox_hardening.sql";
    private const string MemoryFactScopeConsistencyMigration = "003_memory_fact_scope_consistency.sql";

    [DatabaseFact]
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
            var expectedMigrationNames = GetExpectedMigrationNames(migrationsDirectory);

            Assert.Equal(expectedMigrationNames, firstRun.AppliedMigrations.Select(migration => migration.Name));
            Assert.Empty(secondRun.AppliedMigrations);
            Assert.Equal(expectedMigrationNames, secondRun.SkippedMigrations.Select(migration => migration.Name));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_rejects_checksum_mismatch_for_already_applied_migration()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var migrationsDirectory = CopyMigrationsToTempDirectory();
        var databaseName = $"memorysystem_checksum_mismatch_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, migrationsDirectory);
            await File.AppendAllTextAsync(
                Path.Combine(migrationsDirectory, InitialMigration),
                $"{Environment.NewLine}-- checksum mismatch regression test{Environment.NewLine}");

            var exception = await Assert.ThrowsAsync<SqlMigrationChecksumMismatchException>(
                () => SqlMigrationRunner.ApplyAsync(databaseConnectionString, migrationsDirectory));

            Assert.Equal(InitialMigration, exception.MigrationName);
            Assert.NotEqual(exception.RecordedChecksumSha256, exception.CurrentChecksumSha256);
            Assert.Contains("already been applied", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(migrationsDirectory, recursive: true);
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
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

    [DatabaseFact]
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

            Assert.Equal(GetExpectedMigrationNames(migrationsDirectory), exception.MigrationNames);
            Assert.Contains("schema_migrations", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(emptyMigrationsDirectory, recursive: true);
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_rejects_non_contiguous_recorded_migration_history()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var migrationsDirectory = MigrationTestPaths.FindMigrationsDirectory();
        var databaseName = $"memorysystem_migration_gap_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(
                """
                CREATE TABLE schema_migrations (
                    migration_name text PRIMARY KEY,
                    checksum_sha256 text NOT NULL,
                    applied_at timestamptz NOT NULL DEFAULT now()
                );

                INSERT INTO schema_migrations (migration_name, checksum_sha256)
                VALUES
                    (@initial_migration, 'sha256-initial-placeholder'),
                    (@third_migration, 'sha256-third-placeholder');
                """,
                connection);
            command.Parameters.AddWithValue("initial_migration", InitialMigration);
            command.Parameters.AddWithValue("third_migration", MemoryFactScopeConsistencyMigration);
            await command.ExecuteNonQueryAsync();

            var exception = await Assert.ThrowsAsync<SqlMigrationHistoryGapException>(
                () => SqlMigrationRunner.ApplyAsync(databaseConnectionString, migrationsDirectory));

            Assert.Equal(ScopeHardeningMigration, exception.ExpectedMigrationName);
            Assert.Equal(MemoryFactScopeConsistencyMigration, exception.RecordedMigrationName);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
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

    private static string CopyMigrationsToTempDirectory()
    {
        var sourceDirectory = MigrationTestPaths.FindMigrationsDirectory();
        var targetDirectory = Directory.CreateTempSubdirectory("memorysystem-migrations-copy-").FullName;

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*.sql", SearchOption.TopDirectoryOnly))
        {
            File.Copy(sourceFile, Path.Combine(targetDirectory, Path.GetFileName(sourceFile)));
        }

        return targetDirectory;
    }

    private static string[] GetExpectedMigrationNames(string migrationsDirectory)
    {
        return Directory
            .EnumerateFiles(migrationsDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(ParseMigrationOrdinal)
            .ThenBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray();
    }

    private static int ParseMigrationOrdinal(string migrationName)
    {
        var separatorIndex = migrationName.IndexOf('_', StringComparison.Ordinal);

        return int.Parse(migrationName[..separatorIndex]);
    }
}
