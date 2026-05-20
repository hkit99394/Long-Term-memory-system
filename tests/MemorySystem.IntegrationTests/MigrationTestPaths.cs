namespace MemorySystem.IntegrationTests;

internal static class MigrationTestPaths
{
    public static string FindMigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "migrations");

            if (File.Exists(Path.Combine(candidate, "001_initial_memory_schema.sql")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository migrations directory.");
    }
}
