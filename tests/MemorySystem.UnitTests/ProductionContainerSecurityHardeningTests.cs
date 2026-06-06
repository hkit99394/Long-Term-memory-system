namespace MemorySystem.UnitTests;

public sealed class ProductionContainerSecurityHardeningTests
{
    [Fact]
    public void Production_container_preflight_requires_explicit_public_bind_overrides()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "production-container.sh"));

        Assert.Contains("validate_loopback_or_explicit_public_bind", script, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_ALLOW_UNSAFE_PUBLIC_API_BIND", script, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_ALLOW_UNSAFE_PUBLIC_LOCAL_ACCESS_BIND", script, StringComparison.Ordinal);
        Assert.Contains("Invalid %s=0.0.0.0", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Warning: MEMORYSYSTEM_API_BIND=0.0.0.0 exposes the API container port directly.", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_container_preflight_rejects_broad_forward_proxy_networks_without_explicit_override()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "production-container.sh"));
        var envExample = File.ReadAllText(Path.Combine(root, ".env.production.example"));

        Assert.Contains("validate_forward_proxy_network", script, StringComparison.Ordinal);
        Assert.Contains("is_broad_forward_proxy_network", script, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_ALLOW_UNSAFE_BROAD_FORWARD_PROXY_NETWORK", script, StringComparison.Ordinal);
        Assert.Contains("172.16.0.0/12", script, StringComparison.Ordinal);
        Assert.Contains("broad private proxy ranges are rejected", script, StringComparison.Ordinal);
        Assert.DoesNotContain("MEMORYSYSTEM_FORWARD_PROXY_NETWORK=172.16.0.0/12", envExample, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_FORWARD_PROXY_NETWORK=172.30.42.0/24", envExample, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_smoke_scripts_do_not_default_production_usage_to_public_demo_key()
    {
        var root = FindRepositoryRoot();
        var operationsSmoke = File.ReadAllText(Path.Combine(root, "scripts", "operations-metrics-smoke.sh"));
        var pilotSmoke = File.ReadAllText(Path.Combine(root, "scripts", "production-pilot-deployment-smoke.sh"));

        Assert.Contains("LOCAL_DEMO_API_KEY=\"private-alpha-local-key\"", operationsSmoke, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_API_KEY is required when running operations metrics smoke against a non-loopback API.", operationsSmoke, StringComparison.Ordinal);
        Assert.Contains("Refusing to use the public local demo API key against a non-loopback API.", operationsSmoke, StringComparison.Ordinal);

        Assert.DoesNotContain("API_KEY=\"${MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_API_KEY:-private-alpha-local-key}\"", pilotSmoke, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_API_KEY is required", pilotSmoke, StringComparison.Ordinal);
        Assert.Contains("Refusing to run production-pilot deployment smoke with the public local demo API key.", pilotSmoke, StringComparison.Ordinal);
    }

    [Fact]
    public void Caddy_configs_set_content_security_and_frame_hardening_headers()
    {
        var root = FindRepositoryRoot();

        foreach (var caddyFile in new[] { "Caddyfile.production", "Caddyfile.local-access" })
        {
            var text = File.ReadAllText(Path.Combine(root, "ops", "caddy", caddyFile));

            Assert.Contains("Content-Security-Policy", text, StringComparison.Ordinal);
            Assert.Contains("default-src 'self'", text, StringComparison.Ordinal);
            Assert.Contains("script-src 'self'", text, StringComparison.Ordinal);
            Assert.Contains("style-src 'self'", text, StringComparison.Ordinal);
            Assert.Contains("frame-ancestors 'none'", text, StringComparison.Ordinal);
            Assert.Contains("X-Frame-Options \"DENY\"", text, StringComparison.Ordinal);
            Assert.Contains("Permissions-Policy", text, StringComparison.Ordinal);
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
}
