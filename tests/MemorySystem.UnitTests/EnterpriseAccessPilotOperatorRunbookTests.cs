namespace MemorySystem.UnitTests;

public sealed partial class EnterpriseAccessImplementationTests
{
    [Fact]
    public void Pilot_operator_runbook_is_documented_and_linked()
    {
        var root = FindRepositoryRoot();
        var runbook = File.ReadAllText(Path.Combine(root, "docs", "enterprise-access-pilot-operator-runbook.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var enterpriseGate = File.ReadAllText(Path.Combine(root, "docs", "enterprise-access-gate.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var productionSecrets = File.ReadAllText(Path.Combine(root, "docs", "production-secrets.md"));

        Assert.Contains("# Enterprise Access Pilot Operator Runbook", runbook, StringComparison.Ordinal);
        Assert.Contains("Status: EA-09 pilot runbook", runbook, StringComparison.Ordinal);
        Assert.Contains("Authentication resolves an internal principal.", runbook, StringComparison.Ordinal);
        Assert.Contains("Authorization still uses local memberships", runbook, StringComparison.Ordinal);

        foreach (var heading in new[]
                 {
                     "## Provider Setup",
                     "## Identity Binding",
                     "## Access Setup",
                     "## Service-Account Bootstrap",
                     "## Audit Export",
                     "## Rollback",
                     "## Break-Glass API-Key Handling",
                     "## Pilot Closeout"
                 })
        {
            Assert.Contains(heading, runbook, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
                 {
                     "Authentication:Oidc:Enabled=true",
                     "Authentication:Oidc:Issuer=<issuer>",
                     "Authentication:Oidc:Audience=<audience>",
                     "Authentication:Oidc:JwksUri=<https-jwks-uri>",
                     "Authentication:ApiKey:Keys:{keyId}:CredentialId=<service-credential-guid>",
                     "INSERT INTO identity_bindings",
                     "INSERT INTO service_accounts",
                     "INSERT INTO service_account_credentials",
                     "/api/admin/access/organization-memberships",
                     "/api/admin/access/project-memberships",
                     "/api/admin/access/role-assignments",
                     "/api/admin/access/namespace-grants",
                     "/api/admin/access/effective-preview",
                     "/api/admin/audit-exports",
                     "./scripts/enterprise-access-migration-rollback-smoke.sh",
                     "Authentication:Oidc:Enabled=false",
                     "OIDC claims, directory groups, and service credentials must never create memory",
                     "access implicitly."
                 })
        {
            Assert.Contains(requiredText, runbook, StringComparison.Ordinal);
        }

        Assert.Contains(
            "[Enterprise Access Pilot Operator Runbook](enterprise-access-pilot-operator-runbook.md)",
            docsIndex,
            StringComparison.Ordinal);
        Assert.Contains("| EA-09 | P1 | Done | Document pilot operator runbook.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-09 | P1 | Done | Document pilot operator runbook.", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("[Enterprise Access Pilot Operator Runbook](enterprise-access-pilot-operator-runbook.md)", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("EA-09 adds the pilot operator", productPlan, StringComparison.Ordinal);
        Assert.Contains("runbook for OIDC provider setup", productPlan, StringComparison.Ordinal);
        Assert.Contains("EA-10 evaluates directory sync", productPlan, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-07`", productPlan, StringComparison.Ordinal);
        Assert.Contains("Authentication:ApiKey:Keys:{keyId}:CredentialId", productionSecrets, StringComparison.Ordinal);
    }
}
