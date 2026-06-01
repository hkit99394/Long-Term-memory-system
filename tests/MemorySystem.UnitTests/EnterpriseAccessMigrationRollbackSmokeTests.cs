namespace MemorySystem.UnitTests;

public sealed partial class EnterpriseAccessImplementationTests
{
    [Fact]
    public void Migration_and_rollback_smoke_contract_is_documented_and_guarded()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "enterprise-access-migration-rollback-smoke.sh"));
        var integrationTest = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "MemorySystem.IntegrationTests",
            "ApiEnterpriseAccessMigrationRollbackSmokeTests.cs"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var enterpriseGate = File.ReadAllText(Path.Combine(root, "docs", "enterprise-access-gate.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));

        Assert.Contains("ApiEnterpriseAccessMigrationRollbackSmokeTests", script, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true", script, StringComparison.Ordinal);
        Assert.Contains("api-key-only, oidc-only, dual-auth, service-account, oidc-disabled-rollback", script, StringComparison.Ordinal);
        Assert.Contains("memory_access_grants fingerprint unchanged", script, StringComparison.Ordinal);

        Assert.Contains("SmokeAuthMode.ApiKeyOnly", integrationTest, StringComparison.Ordinal);
        Assert.Contains("SmokeAuthMode.OidcOnly", integrationTest, StringComparison.Ordinal);
        Assert.Contains("SmokeAuthMode.DualAuth", integrationTest, StringComparison.Ordinal);
        Assert.Contains("CreateServiceAccountFactory", integrationTest, StringComparison.Ordinal);
        Assert.Contains("SmokeAuthMode.OidcDisabledRollback", integrationTest, StringComparison.Ordinal);
        Assert.Contains("ReadMemoryAccessGrantFingerprintAsync", integrationTest, StringComparison.Ordinal);
        Assert.Contains("Assert.Equal(grantFingerprintBefore, grantFingerprintAfter)", integrationTest, StringComparison.Ordinal);
        Assert.Contains("AssertMemoryReadAsync", integrationTest, StringComparison.Ordinal);
        Assert.Contains("Assert.Equal(HttpStatusCode.NotFound, restrictedResponse.StatusCode)", integrationTest, StringComparison.Ordinal);

        Assert.Contains("| EA-08 | P0 | Done | Add migration and rollback smoke.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-08 | P0 | Done | Add migration and rollback smoke.", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("./scripts/enterprise-access-migration-rollback-smoke.sh", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("| EA-09 | P1 | Done | Document pilot operator runbook.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-10 | P1 | Done | Evaluate directory sync.", backlog, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-07`", productPlan, StringComparison.Ordinal);
    }
}
