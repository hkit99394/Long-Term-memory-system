using MemorySystem.Infrastructure.Migrations;

namespace MemorySystem.UnitTests;

public sealed class SqlMigrationRunnerOptionsTests
{
    private const string UnusedConnectionString = "Host=unused;Database=unused;Username=unused;Password=unused";

    [Fact]
    public async Task ApplyAsync_rejects_non_positive_advisory_lock_timeout_before_reading_migrations()
    {
        var options = new SqlMigrationRunnerOptions
        {
            AdvisoryLockTimeout = TimeSpan.Zero,
            AdvisoryLockRetryDelay = TimeSpan.FromMilliseconds(1)
        };

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => SqlMigrationRunner.ApplyAsync(UnusedConnectionString, MissingDirectory(), options));

        Assert.Equal(nameof(SqlMigrationRunnerOptions.AdvisoryLockTimeout), exception.ParamName);
    }

    [Fact]
    public async Task ApplyAsync_rejects_non_positive_advisory_lock_retry_delay_before_reading_migrations()
    {
        var options = new SqlMigrationRunnerOptions
        {
            AdvisoryLockTimeout = TimeSpan.FromMilliseconds(1),
            AdvisoryLockRetryDelay = TimeSpan.Zero
        };

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => SqlMigrationRunner.ApplyAsync(UnusedConnectionString, MissingDirectory(), options));

        Assert.Equal(nameof(SqlMigrationRunnerOptions.AdvisoryLockRetryDelay), exception.ParamName);
    }

    [Fact]
    public async Task ApplyAsync_rejects_missing_migrations_directory_before_opening_connection()
    {
        var missingDirectory = MissingDirectory();

        var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => SqlMigrationRunner.ApplyAsync(UnusedConnectionString, missingDirectory));

        Assert.Contains(missingDirectory, exception.Message);
    }

    [Fact]
    public async Task ApplyAsync_rejects_current_migration_directory_that_does_not_start_at_initial_migration()
    {
        var migrationsDirectory = Directory.CreateTempSubdirectory("memorysystem-offset-migrations-").FullName;

        try
        {
            await File.WriteAllTextAsync(Path.Combine(migrationsDirectory, "002_second.sql"), "SELECT 2;");

            var exception = await Assert.ThrowsAsync<SqlMigrationHistoryGapException>(
                () => SqlMigrationRunner.ApplyAsync(UnusedConnectionString, migrationsDirectory));

            Assert.Equal("001_*.sql", exception.ExpectedMigrationName);
            Assert.Equal("002_second.sql", exception.RecordedMigrationName);
        }
        finally
        {
            Directory.Delete(migrationsDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyAsync_rejects_current_migration_directory_gaps_before_opening_connection()
    {
        var migrationsDirectory = Directory.CreateTempSubdirectory("memorysystem-gap-migrations-").FullName;

        try
        {
            await File.WriteAllTextAsync(Path.Combine(migrationsDirectory, "001_initial.sql"), "SELECT 1;");
            await File.WriteAllTextAsync(Path.Combine(migrationsDirectory, "002_next.sql"), "SELECT 2;");
            await File.WriteAllTextAsync(Path.Combine(migrationsDirectory, "004_gap.sql"), "SELECT 4;");

            var exception = await Assert.ThrowsAsync<SqlMigrationHistoryGapException>(
                () => SqlMigrationRunner.ApplyAsync(UnusedConnectionString, migrationsDirectory));

            Assert.Equal("003_*.sql", exception.ExpectedMigrationName);
            Assert.Equal("004_gap.sql", exception.RecordedMigrationName);
        }
        finally
        {
            Directory.Delete(migrationsDirectory, recursive: true);
        }
    }

    private static string MissingDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "memory-system-missing-migrations", Guid.NewGuid().ToString("N"));
    }
}
