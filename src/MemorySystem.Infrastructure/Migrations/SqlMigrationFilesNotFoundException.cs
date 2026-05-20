namespace MemorySystem.Infrastructure.Migrations;

public sealed class SqlMigrationFilesNotFoundException(string migrationsDirectory) : InvalidOperationException(
    $"No .sql migration files were found in migration directory '{migrationsDirectory}'.")
{
    public string MigrationsDirectory { get; } = migrationsDirectory;
}
