using MemorySystem.Infrastructure.Configuration;

internal sealed record PrivateAlphaSeedOptions(
    string ConnectionString,
    string MigrationsDirectory,
    bool ApplyMigrations,
    bool SeedEmbeddings,
    bool IncludeBenchmarkOverlays,
    bool ShowHelp)
{
    public static PrivateAlphaSeedOptions Parse(string[] args)
    {
        string? connectionString = null;
        string? migrationsDirectory = null;
        var applyMigrations = true;
        var seedEmbeddings = true;
        var includeBenchmarkOverlays = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "-h":
                case "--help":
                    return new PrivateAlphaSeedOptions(
                        string.Empty,
                        string.Empty,
                        ApplyMigrations: false,
                        SeedEmbeddings: false,
                        IncludeBenchmarkOverlays: false,
                        ShowHelp: true);

                case "--connection-string":
                    connectionString = ReadValue(args, ref index, "--connection-string");
                    break;

                case "--migrations-directory":
                    migrationsDirectory = ReadValue(args, ref index, "--migrations-directory");
                    break;

                case "--skip-migrations":
                    applyMigrations = false;
                    break;

                case "--skip-embeddings":
                    seedEmbeddings = false;
                    break;

                case "--include-benchmark-overlays":
                    includeBenchmarkOverlays = true;
                    break;

                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'. Use --help for usage.");
            }
        }

        return new PrivateAlphaSeedOptions(
            ResolveConnectionString(connectionString),
            Path.GetFullPath(migrationsDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "migrations")),
            applyMigrations,
            seedEmbeddings,
            includeBenchmarkOverlays,
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

    private static string ResolveConnectionString(string? configuredConnectionString)
    {
        return PostgresConnectionString.ResolveLocalDefaults(
            Environment.GetEnvironmentVariable,
            configuredConnectionString);
    }
}
