using MemorySystem.Infrastructure.Migrations;

public static class PrivateAlphaSeedCli
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error)
    {
        try
        {
            var options = PrivateAlphaSeedOptions.Parse(args);

            if (options.ShowHelp)
            {
                output.WriteLine("""
                    Seeds the private-alpha Scenario 0001 demo data into PostgreSQL.

                    Options:
                      --connection-string <value>      PostgreSQL connection string.
                      --migrations-directory <path>    Directory containing ordered .sql migration files.
                      --skip-migrations                Do not apply migrations before seeding.
                      --skip-embeddings                Do not precompute deterministic embeddings for seeded chunks.
                      --include-benchmark-overlays     Add repeatable benchmark-only fixtures on top of Scenario 0001.
                      -h, --help                       Show help.

                    Defaults:
                      --connection-string falls back to MEMORYSYSTEM_POSTGRES_CONNECTION_STRING, then local Docker Compose values.
                      --migrations-directory defaults to ./migrations from the current working directory.
                    """);

                return 0;
            }

            if (options.ApplyMigrations)
            {
                var migrationResult = await SqlMigrationRunner.ApplyAsync(
                    options.ConnectionString,
                    options.MigrationsDirectory);

                output.WriteLine(
                    $"Applied {migrationResult.AppliedCount} migration(s); skipped {migrationResult.SkippedCount} already-applied migration(s).");
            }

            var result = await Scenario0001Seeder.SeedAsync(
                options.ConnectionString,
                options.SeedEmbeddings,
                options.IncludeBenchmarkOverlays);

            output.WriteLine("Scenario 0001 private-alpha demo data is ready.");
            if (options.IncludeBenchmarkOverlays)
            {
                output.WriteLine("benchmark_overlays: fact_finding_contradiction_overlay");
            }
            output.WriteLine($"principal: {Scenario0001.PrincipalId}");
            output.WriteLine($"project: {Scenario0001.ProjectAId}");
            output.WriteLine($"events: {result.EventCount}");
            output.WriteLine($"memory_facts: {result.MemoryFactCount}");
            output.WriteLine($"role_memory_lenses: {result.RoleMemoryLensCount}");
            output.WriteLine($"memory_chunks: {result.MemoryChunkCount}");
            output.WriteLine($"memory_embeddings: {result.MemoryEmbeddingCount}");

            return 0;
        }
        catch (Exception exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
    }
}
