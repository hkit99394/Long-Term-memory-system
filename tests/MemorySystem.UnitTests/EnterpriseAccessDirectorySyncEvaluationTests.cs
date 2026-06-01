namespace MemorySystem.UnitTests;

public sealed partial class EnterpriseAccessImplementationTests
{
    [Fact]
    public void Directory_sync_evaluation_is_documented_as_provisioning_only()
    {
        var root = FindRepositoryRoot();
        var evaluation = File.ReadAllText(Path.Combine(root, "docs", "enterprise-directory-sync-evaluation-ea10.md"));
        var decision = File.ReadAllText(Path.Combine(root, "docs", "decisions", "0047-directory-sync-provisioning-only.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var enterpriseGate = File.ReadAllText(Path.Combine(root, "docs", "enterprise-access-gate.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));

        Assert.Contains("# Enterprise Directory Sync Evaluation EA-10", evaluation, StringComparison.Ordinal);
        Assert.Contains("Status: Accepted evaluation", evaluation, StringComparison.Ordinal);
        Assert.Contains("Do not add SCIM or provider-specific directory sync before the first enterprise", evaluation, StringComparison.Ordinal);
        Assert.Contains("must be provisioning-only", evaluation, StringComparison.Ordinal);
        Assert.Contains("must not directly grant memory read, write, review, or admin access", evaluation, StringComparison.Ordinal);
        Assert.Contains("IMemoryAccessAuthorizer", evaluation, StringComparison.Ordinal);
        Assert.Contains("## Allowed Future Sync Behavior", evaluation, StringComparison.Ordinal);
        Assert.Contains("## Disallowed Future Sync Behavior", evaluation, StringComparison.Ordinal);
        Assert.Contains("## Final EA-10 Outcome", evaluation, StringComparison.Ordinal);
        Assert.Contains("The next project cleanup was `DM-06`", evaluation, StringComparison.Ordinal);

        foreach (var disallowed in new[]
                 {
                     "authorize memory reads or writes from token group claims at request time",
                     "grant `read`, `write`, `review`, or `admin` access directly from provider",
                     "bypass admin effective-access preview semantics",
                     "skip audit records for access-affecting changes"
                 })
        {
            Assert.Contains(disallowed, evaluation, StringComparison.Ordinal);
        }

        Assert.Contains("# Decision 0047: Directory Sync Is Provisioning Only", decision, StringComparison.Ordinal);
        Assert.Contains("Status: Accepted", decision, StringComparison.Ordinal);
        Assert.Contains("Do not implement directory sync before the first enterprise pilot.", decision, StringComparison.Ordinal);
        Assert.Contains("Directory groups, OIDC token roles, token scopes, and provider claims must not", decision, StringComparison.Ordinal);
        Assert.Contains("directly grant memory read, write, review, or admin access at request time.", decision, StringComparison.Ordinal);

        Assert.Contains(
            "[Enterprise Directory Sync Evaluation EA-10](enterprise-directory-sync-evaluation-ea10.md)",
            docsIndex,
            StringComparison.Ordinal);
        Assert.Contains(
            "[Decision 0047: Directory Sync Is Provisioning Only](decisions/0047-directory-sync-provisioning-only.md)",
            docsIndex,
            StringComparison.Ordinal);
        Assert.Contains("| EA-10 | P1 | Done | Evaluate directory sync.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-10 | P1 | Done | Evaluate directory sync.", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("| DM-06 | P2 | Done | Remove duplicate string normalization helpers.", backlog, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-07`", productPlan, StringComparison.Ordinal);
    }
}
