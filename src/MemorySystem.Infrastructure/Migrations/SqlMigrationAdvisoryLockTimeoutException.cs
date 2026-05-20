namespace MemorySystem.Infrastructure.Migrations;

public sealed class SqlMigrationAdvisoryLockTimeoutException(TimeSpan timeout) : TimeoutException(
    $"Timed out after {timeout} while waiting for the SQL migration advisory lock.")
{
    public TimeSpan Timeout { get; } = timeout;
}
