namespace MemorySystem.UnitTests;

public sealed class OrganizationProjectManagementOpm07Tests
{
    [Fact]
    public void Opm07_management_activity_timeline_is_documented_guarded_and_payload_safe()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var opm01 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm01.md"));
        var opm07 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm07.md"));
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementEndpointExtensions.cs"));
        var responses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementResponses.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Admin", "IAdminManagementActivityStore.cs"));
        var registration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "ApiAdminServiceCollectionExtensions.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminManagementActivityStore.cs"));
        var sourcePanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "management-panel.ts"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var css = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.css"));

        Assert.Contains("| OPM-07 | P1 | Done | Product Owner + Security Professional + Developer | Management Activity Timeline |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| OPM-07 | P1 | Done | Add management activity timeline. |", backlog, StringComparison.Ordinal);
        Assert.Contains("[Organization And Project Management OPM-07](organization-project-management-opm07.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Status: OPM-01 through OPM-08 implemented.", opm01, StringComparison.Ordinal);
        Assert.Contains("# Organization And Project Management OPM-07", opm07, StringComparison.Ordinal);
        Assert.Contains("OrganizationProjectManagementOpm07Tests", testing, StringComparison.Ordinal);
        Assert.Contains("ApiAdminManagementActivityTests", testing, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "/api/admin/organizations/{organizationId:guid}/management-activity",
                     "/api/admin/projects/{projectId:guid}/management-activity",
                     "IAdminManagementActivityStore",
                     "AuthorizeOrganizationManagementAsync",
                     "AuthorizeProjectManagementAsync"
                 })
        {
            Assert.Contains(fragment, endpoint, StringComparison.Ordinal);
        }

        Assert.Contains("AdminManagementActivityResponse", responses, StringComparison.Ordinal);
        Assert.Contains("AdminManagementActivityEntryResponse", responses, StringComparison.Ordinal);
        Assert.Contains("PayloadSafe", responses, StringComparison.Ordinal);
        Assert.Contains("RawSourcePayloadsIncluded", responses, StringComparison.Ordinal);
        Assert.Contains("IAdminManagementActivityStore", contract, StringComparison.Ordinal);
        Assert.Contains("AdminManagementActivityEntryRecord", contract, StringComparison.Ordinal);
        Assert.Contains("IAdminManagementActivityStore, PostgresAdminManagementActivityStore", registration, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "private const string ContractId = \"OPM-07\"",
                     "access_audit_events",
                     "jsonb_strip_nulls(jsonb_build_object",
                     "AccessAuditActionTypes.ProjectRegistration",
                     "AccessAuditActionTypes.ProjectLifecycleChange",
                     "AccessAuditActionTypes.ProjectScopeSettingsChange",
                     "AccessAuditActionTypes.NamespaceGrantChange",
                     "auditEvidenceId",
                     "sourceHashCoveragePercent",
                     "previousGrantCount",
                     "newGrantCount"
                 })
        {
            Assert.Contains(fragment, store, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("'reason'", store, StringComparison.Ordinal);
        Assert.DoesNotContain("audit_metadata::text", store, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "managementActivityDetail",
                     "managementActivitySection",
                     "managementActivityEntry",
                     "/management-activity?limit=20",
                     "Management activity API",
                     "managementActivityEntryCount"
                 })
        {
            Assert.Contains(fragment, sourcePanel, StringComparison.Ordinal);
            Assert.Contains(fragment, script, StringComparison.Ordinal);
        }

        Assert.Contains(".management-activity", css, StringComparison.Ordinal);
        Assert.Contains(".management-activity-entry", css, StringComparison.Ordinal);
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
