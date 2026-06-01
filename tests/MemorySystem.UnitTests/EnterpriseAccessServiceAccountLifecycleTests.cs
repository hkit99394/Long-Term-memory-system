namespace MemorySystem.UnitTests;

public sealed partial class EnterpriseAccessImplementationTests
{
    [Fact]
    public void Service_account_lifecycle_contract_is_documented_and_guarded()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "migrations", "028_service_account_lifecycle.sql"));
        var restoreManifest = File.ReadAllText(Path.Combine(root, "scripts", "restore-validation-tables.txt"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "ServiceAccounts", "IServiceAccountLifecycleStore.cs"));
        var commands = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "ServiceAccounts", "ServiceAccountCommands.cs"));
        var records = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "ServiceAccounts", "ServiceAccountRecords.cs"));
        var statuses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "ServiceAccounts", "ServiceAccountStatuses.cs"));
        var credentialStatuses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "ServiceAccounts", "ServiceAccountCredentialStatuses.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "ServiceAccounts", "PostgresServiceAccountLifecycleStore.cs"));
        var accessRegistration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Access", "ApiAccessServiceCollectionExtensions.cs"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var enterpriseGate = File.ReadAllText(Path.Combine(root, "docs", "enterprise-access-gate.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));

        Assert.Contains("CREATE TABLE service_accounts", migration, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE service_account_credentials", migration, StringComparison.Ordinal);
        Assert.Contains("validate_service_account_principal", migration, StringComparison.Ordinal);
        Assert.Contains("target_principal_type <> 'service'", migration, StringComparison.Ordinal);
        Assert.Contains("allowed_auth_method IN ('api_key', 'oidc', 'service_account')", migration, StringComparison.Ordinal);
        Assert.Contains("review_due_at IS NOT NULL OR expires_at IS NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ux_service_account_credentials_active_fingerprint", migration, StringComparison.Ordinal);
        Assert.Contains("status IN ('active', 'rotated', 'disabled', 'expired')", migration, StringComparison.Ordinal);

        Assert.Contains("service_accounts", restoreManifest, StringComparison.Ordinal);
        Assert.Contains("service_account_credentials", restoreManifest, StringComparison.Ordinal);

        Assert.Contains("IServiceAccountLifecycleStore", contract, StringComparison.Ordinal);
        Assert.Contains("UpsertProfileAsync", contract, StringComparison.Ordinal);
        Assert.Contains("CreateCredentialAsync", contract, StringComparison.Ordinal);
        Assert.Contains("RotateCredentialAsync", contract, StringComparison.Ordinal);
        Assert.Contains("DisableCredentialAsync", contract, StringComparison.Ordinal);
        Assert.Contains("GrantNamespaceAsync", contract, StringComparison.Ordinal);
        Assert.Contains("ServiceAccountProfileCommand", commands, StringComparison.Ordinal);
        Assert.Contains("AllowedAuthMethod", commands, StringComparison.Ordinal);
        Assert.Contains("ReviewDueAt", records, StringComparison.Ordinal);
        Assert.Contains("public const string Active = \"active\"", statuses, StringComparison.Ordinal);
        Assert.Contains("public const string Rotated = \"rotated\"", credentialStatuses, StringComparison.Ordinal);
        Assert.Contains("public const string Disabled = \"disabled\"", credentialStatuses, StringComparison.Ordinal);

        Assert.Contains("AccessAuditActionTypes.ServiceCredentialChange", store, StringComparison.Ordinal);
        Assert.Contains("AccessAuditActionTypes.NamespaceGrantChange", store, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO memory_access_grants", store, StringComparison.Ordinal);
        Assert.Contains("MemoryAccessPermissions.Admin", store, StringComparison.Ordinal);
        Assert.Contains("namespacePrefix is \"/\" or \"/global\"", store, StringComparison.Ordinal);
        Assert.Contains("IServiceAccountLifecycleStore, PostgresServiceAccountLifecycleStore", accessRegistration, StringComparison.Ordinal);

        Assert.Contains("| EA-05 | P0 | Done | Add service-account lifecycle.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-05 | P0 | Done | Add service-account lifecycle.", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-07`", productPlan, StringComparison.Ordinal);
    }
}
