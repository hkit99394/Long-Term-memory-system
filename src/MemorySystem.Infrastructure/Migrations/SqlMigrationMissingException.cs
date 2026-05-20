namespace MemorySystem.Infrastructure.Migrations;

public sealed class SqlMigrationMissingException : InvalidOperationException
{
    public SqlMigrationMissingException(IReadOnlyList<string> migrationNames)
        : base(BuildMessage(migrationNames))
    {
        MigrationNames = migrationNames;
    }

    public IReadOnlyList<string> MigrationNames { get; }

    private static string BuildMessage(IReadOnlyList<string> migrationNames)
    {
        ArgumentNullException.ThrowIfNull(migrationNames);

        if (migrationNames.Count == 0)
        {
            throw new ArgumentException("At least one migration name is required.", nameof(migrationNames));
        }

        if (migrationNames.Count == 1)
        {
            return $"Migration '{migrationNames[0]}' is recorded in schema_migrations but no matching .sql file exists in the migrations directory.";
        }

        return "The following migrations are recorded in schema_migrations but no matching .sql files exist in the migrations directory: " +
            string.Join(", ", migrationNames.Select(name => $"'{name}'")) +
            ".";
    }
}
