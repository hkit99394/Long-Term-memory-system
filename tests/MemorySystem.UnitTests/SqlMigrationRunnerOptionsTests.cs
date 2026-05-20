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

    private static string MissingDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "memory-system-missing-migrations", Guid.NewGuid().ToString("N"));
    }
}
