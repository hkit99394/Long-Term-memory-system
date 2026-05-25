namespace MemorySystem.Infrastructure.Migrations;

public sealed class SqlMigrationHistoryGapException(
    string expectedMigrationName,
    string recordedMigrationName) : InvalidOperationException(
    $"Migration history is not contiguous. Expected migration '{expectedMigrationName}' before '{recordedMigrationName}'.")
{
    public string ExpectedMigrationName { get; } = expectedMigrationName;

    public string RecordedMigrationName { get; } = recordedMigrationName;
}
