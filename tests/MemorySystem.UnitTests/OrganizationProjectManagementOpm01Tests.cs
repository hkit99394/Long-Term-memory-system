namespace MemorySystem.UnitTests;

public sealed class OrganizationProjectManagementOpm01Tests
{
    [Fact]
    public void Opm01_admin_org_project_read_model_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementEndpointExtensions.cs"));
        var responses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementResponses.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Admin", "IAdminOrganizationProjectManagementStore.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminOrganizationProjectManagementStore.cs"));
        var registration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "ApiAdminServiceCollectionExtensions.cs"));
        var program = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Program.cs"));
        var integration = File.ReadAllText(Path.Combine(root, "tests", "MemorySystem.IntegrationTests", "ApiAdminOrganizationProjectManagementTests.cs"));
        var managementPlan = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm01.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));

        foreach (var path in new[]
                 {
                     "/api/admin/organizations",
                     "/api/admin/organizations/{organizationId:guid}",
                     "/api/admin/projects",
                     "/api/admin/projects/{projectId:guid}"
                 })
        {
            Assert.Contains(path, endpoint, StringComparison.Ordinal);
        }

        Assert.Contains("ContractId = \"OPM-01\"", endpoint, StringComparison.Ordinal);
        Assert.Contains("AdminOrganizationListResponse", responses, StringComparison.Ordinal);
        Assert.Contains("AdminProjectDetailResponse", responses, StringComparison.Ordinal);
        Assert.Contains("PayloadSafe", responses, StringComparison.Ordinal);
        Assert.Contains("RawSourcePayloadsIncluded", responses, StringComparison.Ordinal);

        Assert.Contains("IAdminOrganizationProjectManagementStore", contract, StringComparison.Ordinal);
        Assert.Contains("AdminOrganizationListQuery", contract, StringComparison.Ordinal);
        Assert.Contains("AdminProjectRegistrationEvidenceRecord", contract, StringComparison.Ordinal);
        Assert.Contains("PostgresAdminOrganizationProjectManagementStore", store, StringComparison.Ordinal);
        Assert.Contains("organization_memberships", store, StringComparison.Ordinal);
        Assert.Contains("project_memberships", store, StringComparison.Ordinal);
        Assert.Contains("project_role_definitions", store, StringComparison.Ordinal);
        Assert.Contains("memory_access_grants", store, StringComparison.Ordinal);
        Assert.Contains("access_audit_events", store, StringComparison.Ordinal);
        Assert.Contains("LIMIT @limit_plus_one", store, StringComparison.Ordinal);

        Assert.Contains("IAdminOrganizationProjectManagementStore, PostgresAdminOrganizationProjectManagementStore", registration, StringComparison.Ordinal);
        Assert.Contains("MapMemorySystemAdminOrganizationProjectManagementEndpoints", program, StringComparison.Ordinal);
        Assert.Contains("Get_admin_organization_project_management_lists_payload_safe_visible_counts", integration, StringComparison.Ordinal);
        Assert.Contains("without org directory leak", managementPlan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("| OPM-01 | P0 | Done | Product Owner + Developer + Security Professional | Admin Organization/Project Read Model |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| OPM-01 | P0 | Done | Add admin organization/project read model. |", backlog, StringComparison.Ordinal);
        Assert.Contains("OrganizationProjectManagementOpm01Tests", testing, StringComparison.Ordinal);
        Assert.Contains("ApiAdminOrganizationProjectManagementTests", testing, StringComparison.Ordinal);
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
