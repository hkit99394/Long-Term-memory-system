namespace MemorySystem.Infrastructure.Migrations;

public sealed class SqlMigrationRunnerOptions
{
    public static SqlMigrationRunnerOptions Default { get; } = new();

    public TimeSpan AdvisoryLockTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan AdvisoryLockRetryDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    internal void Validate()
    {
        if (AdvisoryLockTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AdvisoryLockTimeout),
                AdvisoryLockTimeout,
                "Advisory lock timeout must be positive.");
        }

        if (AdvisoryLockRetryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AdvisoryLockRetryDelay),
                AdvisoryLockRetryDelay,
                "Advisory lock retry delay must be positive.");
        }
    }
}
