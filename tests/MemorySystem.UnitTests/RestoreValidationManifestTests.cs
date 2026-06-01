using System.Text.RegularExpressions;

namespace MemorySystem.UnitTests;

public sealed partial class RestoreValidationManifestTests
{
    [Fact]
    public void Restore_validation_manifest_tracks_all_database_tables()
    {
        var root = FindRepositoryRoot();
        var manifestPath = Path.Combine(root, "scripts", "restore-validation-tables.txt");
        var migrationDirectory = Path.Combine(root, "migrations");
        var backupSmokePath = Path.Combine(root, "scripts", "backup-restore-smoke.sh");
        var deploymentSmokePath = Path.Combine(root, "scripts", "production-pilot-deployment-smoke.sh");

        var manifestTables = File.ReadAllLines(manifestPath)
            .Select(line => line.Split('#')[0].Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        var migrationTables = Directory
            .EnumerateFiles(migrationDirectory, "*.sql")
            .SelectMany(file => CreateTableRegex()
                .Matches(File.ReadAllText(file))
                .Select(match => match.Groups["table"].Value))
            .Append("schema_migrations")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(migrationTables, manifestTables.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(manifestTables.Length, manifestTables.Distinct(StringComparer.Ordinal).Count());

        Assert.Contains("restore-validation-tables.sh", File.ReadAllText(backupSmokePath), StringComparison.Ordinal);
        var backupSmoke = File.ReadAllText(backupSmokePath);
        Assert.Contains("load_restore_validation_tables", backupSmoke, StringComparison.Ordinal);
        Assert.Contains("Running migrations against source database", backupSmoke, StringComparison.Ordinal);
        Assert.Contains("restore-validation-tables.sh", File.ReadAllText(deploymentSmokePath), StringComparison.Ordinal);
        Assert.Contains("load_restore_validation_tables", File.ReadAllText(deploymentSmokePath), StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MemorySystem.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    [GeneratedRegex(@"\bCREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<table>[a-z_][a-z0-9_]*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CreateTableRegex();
}
