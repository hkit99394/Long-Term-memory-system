using System.Diagnostics;

namespace MemorySystem.UnitTests;

public sealed class ExternalManagedPostgresProfileTests
{
    [Fact]
    public void External_managed_postgres_profile_is_documented_configured_and_smokeable()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var profileDoc = File.ReadAllText(Path.Combine(root, "docs", "external-managed-postgres-profile.md"));
        var productionContainerDoc = File.ReadAllText(Path.Combine(root, "docs", "production-container.md"));
        var productionSecretsDoc = File.ReadAllText(Path.Combine(root, "docs", "production-secrets.md"));
        var testingDoc = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.production.yml"));
        var externalCompose = File.ReadAllText(Path.Combine(root, "docker-compose.production.external-postgres.yml"));
        var envExample = File.ReadAllText(Path.Combine(root, ".env.production.example"));
        var productionScript = File.ReadAllText(Path.Combine(root, "scripts", "production-container.sh"));
        var productionEnvLib = File.ReadAllText(Path.Combine(root, "scripts", "lib", "production-env.sh"));
        var boundarySeed = File.ReadAllText(Path.Combine(root, "scripts", "seed-production-memory-boundary.sh"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "external-postgres-profile-smoke.sh"));
        var productionProfileSurface = productionScript + "\n" + productionEnvLib;

        Assert.Contains("| IP-03 | P0 | Done | IT/Ops + CTO | External / Managed PostgreSQL Production Profile |", productPlan, StringComparison.Ordinal);
        Assert.Contains("[External / Managed PostgreSQL Production Profile](external-managed-postgres-profile.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Status: active production profile for improvement plan item IP-03.", profileDoc, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_POSTGRES_PROFILE=external", profileDoc, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_POSTGRES_CONNECTION_STRING", profileDoc, StringComparison.Ordinal);
        Assert.Contains("scripts/production-container.sh migrate", profileDoc, StringComparison.Ordinal);
        Assert.Contains("/app/scripts/platform-restore-validation.sh", profileDoc, StringComparison.Ordinal);
        Assert.Contains("./scripts/external-postgres-profile-smoke.sh", profileDoc, StringComparison.Ordinal);

        Assert.Contains("MEMORYSYSTEM_POSTGRES_PROFILE=local", envExample, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_POSTGRES_CONNECTION_STRING=", envExample, StringComparison.Ordinal);
        Assert.Contains("SSL Mode=VerifyFull", envExample, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_POSTGRES_URL=", envExample, StringComparison.Ordinal);

        Assert.Contains("MEMORYSYSTEM_POSTGRES_CONNECTION_STRING: ${MEMORYSYSTEM_POSTGRES_CONNECTION_STRING:-}", compose, StringComparison.Ordinal);
        Assert.Contains("docker-compose.production.external-postgres.yml", productionProfileSurface, StringComparison.Ordinal);
        Assert.Contains("validate_external_postgres_connection_string", productionProfileSurface, StringComparison.Ordinal);
        Assert.Contains("runtime_services", productionProfileSurface, StringComparison.Ordinal);
        Assert.Contains("SSL Mode=Require, VerifyCA, or VerifyFull", productionProfileSurface, StringComparison.Ordinal);

        Assert.Contains("profiles:", externalCompose, StringComparison.Ordinal);
        Assert.Contains("local-postgres", externalCompose, StringComparison.Ordinal);
        Assert.Contains("depends_on: !reset []", externalCompose, StringComparison.Ordinal);
        Assert.Contains("Set MEMORYSYSTEM_POSTGRES_CONNECTION_STRING for MEMORYSYSTEM_POSTGRES_PROFILE=external", externalCompose, StringComparison.Ordinal);

        Assert.Contains("is_external_postgres_profile", boundarySeed, StringComparison.Ordinal);
        Assert.Contains("psql \"$(read_env_value MEMORYSYSTEM_POSTGRES_CONNECTION_STRING \"\")\"", boundarySeed, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_POSTGRES_PROFILE=external", productionContainerDoc, StringComparison.Ordinal);
        Assert.Contains("External / Managed PostgreSQL Production Profile", productionSecretsDoc, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/external-postgres-profile-smoke.sh", testingDoc, StringComparison.Ordinal);

        Assert.Contains("MEMORYSYSTEM_POSTGRES_PROFILE=external", smoke, StringComparison.Ordinal);
        Assert.Contains("production-container.sh\" config", smoke, StringComparison.Ordinal);
        Assert.Contains("External PostgreSQL profile smoke passed", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public async Task External_managed_postgres_preflight_accepts_managed_host_with_postgres_prefix()
    {
        var root = FindRepositoryRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"memorysystem-ip03-{Guid.NewGuid():N}");
        var fakeBin = Path.Combine(tempRoot, "bin");
        var envFile = Path.Combine(tempRoot, ".env.production");

        try
        {
            Directory.CreateDirectory(fakeBin);
            File.WriteAllText(
                Path.Combine(fakeBin, "docker"),
                """
                #!/usr/bin/env bash
                set -euo pipefail
                case "$*" in
                  info|compose\ version|image\ inspect*|compose*) exit 0 ;;
                esac
                exit 0
                """);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    Path.Combine(fakeBin, "docker"),
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            File.WriteAllText(
                envFile,
                """
                COMPOSE_PROJECT_NAME=memorysystem-managed-review
                MEMORYSYSTEM_IMAGE=memorysystem:1.0.0
                MEMORYSYSTEM_POSTGRES_PROFILE=external
                MEMORYSYSTEM_POSTGRES_CONNECTION_STRING="Host=postgres-prod.example.internal;Port=5432;Database=memory_system;Username=memory_system;Password=managed-postgres-secret-1234567890;SSL Mode=VerifyFull"
                MEMORYSYSTEM_OPERATOR_API_KEY=managed-profile-operator-key-1234567890
                MEMORYSYSTEM_OPERATOR_PRINCIPAL_ID=11111111-1111-4111-8111-111111111111
                OPENAI_API_KEY=managed-profile-openai-key-1234567890
                MEMORYSYSTEM_FORWARD_PROXY_NETWORK=172.30.42.0/24
                MEMORYSYSTEM_API_BIND=127.0.0.1
                MEMORYSYSTEM_PRODUCTION_TLS_ENABLED=false
                """);

            var result = await RunScriptAsync(
                root,
                ["scripts/production-container.sh", "preflight"],
                new Dictionary<string, string?>
                {
                    ["MEMORYSYSTEM_PRODUCTION_ENV_FILE"] = envFile,
                    ["PATH"] = fakeBin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH")
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Production host preflight passed", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("bad substitution", result.StandardError, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("local Compose database", result.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
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

    private static async Task<ScriptResult> RunScriptAsync(
        string root,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(key);
                }
                else
                {
                    startInfo.Environment[key] = value;
                }
            }
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start production-container script.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ScriptResult(process.ExitCode, await standardOutput, await standardError);
    }

    private sealed record ScriptResult(int ExitCode, string StandardOutput, string StandardError);
}
