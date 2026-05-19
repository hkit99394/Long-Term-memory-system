namespace MemorySystem.Infrastructure.Migrations;

public sealed class SqlMigrationChecksumMismatchException(
    string migrationName,
    string recordedChecksumSha256,
    string currentChecksumSha256) : InvalidOperationException(
        $"Migration '{migrationName}' has already been applied with checksum '{recordedChecksumSha256}', " +
            $"but the current file checksum is '{currentChecksumSha256}'.")
{
    public string MigrationName { get; } = migrationName;

    public string RecordedChecksumSha256 { get; } = recordedChecksumSha256;

    public string CurrentChecksumSha256 { get; } = currentChecksumSha256;
}
