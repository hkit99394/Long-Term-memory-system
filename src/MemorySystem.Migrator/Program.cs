using MemorySystem.Infrastructure.Configuration;
using MemorySystem.Infrastructure.Migrations;

return await MigratorCli.RunAsync(args, Console.Out, Console.Error);

public static class MigratorCli
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error)
    {
        try
        {
            var parseResult = MigratorOptions.Parse(args);

            if (parseResult.ShowHelp)
            {
                output.WriteLine("""
                    Applies ordered SQL migrations from the repository migrations directory.

                    Options:
                      --connection-string <value>      PostgreSQL connection string.
                      --migrations-directory <path>    Directory containing ordered .sql migration files.
                      -h, --help                       Show help.

                    Defaults:
                      --connection-string falls back to MEMORYSYSTEM_POSTGRES_CONNECTION_STRING, then local Docker Compose values only in Development/Testing.
                      --migrations-directory defaults to ./migrations from the current working directory.
                    """);

                return 0;
            }

            var result = await SqlMigrationRunner.ApplyAsync(
                parseResult.ConnectionString,
                parseResult.MigrationsDirectory);

            output.WriteLine($"Applied {result.AppliedCount} migration(s); skipped {result.SkippedCount} already-applied migration(s).");

            foreach (var migration in result.AppliedMigrations)
            {
                output.WriteLine($"applied {migration.Name} {migration.ChecksumSha256}");
            }

            foreach (var migration in result.SkippedMigrations)
            {
                output.WriteLine($"skipped {migration.Name} {migration.ChecksumSha256}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
    }
}

internal sealed record MigratorOptions(
    string ConnectionString,
    string MigrationsDirectory,
    bool ShowHelp)
{
    public static MigratorOptions Parse(string[] args)
    {
        string? connectionString = null;
        string? migrationsDirectory = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "-h":
                case "--help":
                    return new MigratorOptions(string.Empty, string.Empty, ShowHelp: true);

                case "--connection-string":
                    connectionString = ReadValue(args, ref index, "--connection-string");
                    break;

                case "--migrations-directory":
                    migrationsDirectory = ReadValue(args, ref index, "--migrations-directory");
                    break;

                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'. Use --help for usage.");
            }
        }

        return new MigratorOptions(
            connectionString ?? ResolveConnectionString(),
            Path.GetFullPath(migrationsDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "migrations")),
            ShowHelp: false);
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '{optionName}' requires a value.");
        }

        index++;
        return args[index];
    }

    private static string ResolveConnectionString()
    {
        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var effectiveEnvironmentName = string.IsNullOrWhiteSpace(environmentName) ? "Production" : environmentName;

        return PostgresConnectionString.Resolve(Environment.GetEnvironmentVariable, environmentName: effectiveEnvironmentName);
    }
}
