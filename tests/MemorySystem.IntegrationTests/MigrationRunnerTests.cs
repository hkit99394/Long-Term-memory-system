using MemorySystem.Infrastructure.Migrations;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class MigrationRunnerTests
{
    private const long AdvisoryLockKey = 7_404_808_312_433_927_019;
    private const string InitialMigration = "001_initial_memory_schema.sql";
    private const string ScopeHardeningMigration = "002_scope_constraints_and_outbox_hardening.sql";
    private const string MemoryFactScopeConsistencyMigration = "003_memory_fact_scope_consistency.sql";
    private const string MemoryFactTrustLevelMigration = "004_memory_fact_trust_level.sql";
    private const string RoleMemoryLensActiveBaseFactMigration = "005_role_memory_lens_active_base_fact.sql";
    private const string MemoryFactSubjectPredicateLookupMigration = "006_memory_fact_subject_predicate_lookup.sql";
    private const string MemoryFactConflictAndLensSourceScopeMigration = "007_memory_fact_conflict_and_lens_source_scope.sql";
    private const string RoleMemoryLensActiveDedupeMigration = "008_role_memory_lens_active_dedupe.sql";
    private const string MemoryAccessGrantTargetValidationMigration = "009_memory_access_grant_target_validation.sql";
    private const string EventReferenceUpdateGuardsMigration = "010_event_reference_update_guards.sql";
    private const string ApiIdempotencyResponseContentTypeMigration = "011_api_idempotency_response_content_type.sql";
    private const string MemoryChunkFullTextSearchMigration = "012_memory_chunk_full_text_search.sql";
    private const string OutboxProcessingLeaseMetadataMigration = "013_outbox_processing_lease_metadata.sql";
    private const string MemoryEmbeddingVectorIndexMigration = "014_memory_embedding_vector_index.sql";

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

            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == InitialMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == ScopeHardeningMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == MemoryFactScopeConsistencyMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == MemoryFactTrustLevelMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == RoleMemoryLensActiveBaseFactMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == MemoryFactSubjectPredicateLookupMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == MemoryFactConflictAndLensSourceScopeMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == RoleMemoryLensActiveDedupeMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == MemoryAccessGrantTargetValidationMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == EventReferenceUpdateGuardsMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == ApiIdempotencyResponseContentTypeMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == MemoryChunkFullTextSearchMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == OutboxProcessingLeaseMetadataMigration);
            Assert.Contains(firstRun.AppliedMigrations, migration => migration.Name == MemoryEmbeddingVectorIndexMigration);
            Assert.Empty(secondRun.AppliedMigrations);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == InitialMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == ScopeHardeningMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == MemoryFactScopeConsistencyMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == MemoryFactTrustLevelMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == RoleMemoryLensActiveBaseFactMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == MemoryFactSubjectPredicateLookupMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == MemoryFactConflictAndLensSourceScopeMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == RoleMemoryLensActiveDedupeMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == MemoryAccessGrantTargetValidationMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == EventReferenceUpdateGuardsMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == ApiIdempotencyResponseContentTypeMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == MemoryChunkFullTextSearchMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == OutboxProcessingLeaseMetadataMigration);
            Assert.Contains(secondRun.SkippedMigrations, migration => migration.Name == MemoryEmbeddingVectorIndexMigration);
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

            Assert.Contains(InitialMigration, exception.MigrationNames);
            Assert.Contains(ScopeHardeningMigration, exception.MigrationNames);
            Assert.Contains(MemoryFactScopeConsistencyMigration, exception.MigrationNames);
            Assert.Contains(MemoryFactTrustLevelMigration, exception.MigrationNames);
            Assert.Contains(RoleMemoryLensActiveBaseFactMigration, exception.MigrationNames);
            Assert.Contains(MemoryFactSubjectPredicateLookupMigration, exception.MigrationNames);
            Assert.Contains(MemoryFactConflictAndLensSourceScopeMigration, exception.MigrationNames);
            Assert.Contains(RoleMemoryLensActiveDedupeMigration, exception.MigrationNames);
            Assert.Contains(MemoryAccessGrantTargetValidationMigration, exception.MigrationNames);
            Assert.Contains(EventReferenceUpdateGuardsMigration, exception.MigrationNames);
            Assert.Contains(ApiIdempotencyResponseContentTypeMigration, exception.MigrationNames);
            Assert.Contains(MemoryChunkFullTextSearchMigration, exception.MigrationNames);
            Assert.Contains(OutboxProcessingLeaseMetadataMigration, exception.MigrationNames);
            Assert.Contains(MemoryEmbeddingVectorIndexMigration, exception.MigrationNames);
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
}
