namespace MemorySystem.Infrastructure.Migrations;

public sealed record AppliedSqlMigration(string Name, string ChecksumSha256);

public sealed record SqlMigrationRunResult(
    IReadOnlyList<AppliedSqlMigration> AppliedMigrations,
    IReadOnlyList<AppliedSqlMigration> SkippedMigrations)
{
    public int AppliedCount => AppliedMigrations.Count;

    public int SkippedCount => SkippedMigrations.Count;
}
