namespace MemorySystem.UnitTests;

public sealed class ProjectRegistrationReg02Tests
{
    [Fact]
    public void Reg02_project_registration_api_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminProjectRegistrationEndpointExtensions.cs"));
        var requests = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminProjectRegistrationRequests.cs"));
        var responses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminProjectRegistrationResponses.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Admin", "IAdminProjectRegistrationStore.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminProjectRegistrationStore.cs"));
        var registration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "ApiAdminServiceCollectionExtensions.cs"));
        var program = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Program.cs"));
        var auditActions = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "AccessAuditing", "AccessAuditActionTypes.cs"));
        var migration = File.ReadAllText(Path.Combine(root, "migrations", "031_project_registration_api.sql"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var registrationPlan = File.ReadAllText(Path.Combine(root, "docs", "project-registration-ux-plan.md"));

        Assert.Contains("/api/admin/projects/register", endpoint, StringComparison.Ordinal);
        Assert.Contains("ApiIdempotencyHttpService", endpoint, StringComparison.Ordinal);
        Assert.Contains("POST /api/admin/projects/register", endpoint, StringComparison.Ordinal);
        Assert.Contains("AuthorizeRegistrationAsync", endpoint, StringComparison.Ordinal);
        Assert.Contains("MemoryAccessPermissions.Admin", endpoint, StringComparison.Ordinal);
        Assert.Contains("AdminProjectRegistrationRequest", requests, StringComparison.Ordinal);
        Assert.Contains("AdminProjectRegistrationResponse", responses, StringComparison.Ordinal);

        Assert.Contains("IAdminProjectRegistrationStore", contract, StringComparison.Ordinal);
        Assert.Contains("AdminProjectRegistrationCommand", contract, StringComparison.Ordinal);
        Assert.Contains("RegistrationRequestHash", contract, StringComparison.Ordinal);
        Assert.Contains("PayloadSafe", contract, StringComparison.Ordinal);
        Assert.Contains("PostgresAdminProjectRegistrationStore", store, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO organizations", store, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO projects", store, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO project_role_definitions", store, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO project_memberships", store, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO role_assignments", store, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO memory_access_grants", store, StringComparison.Ordinal);
        Assert.Contains("Project root namespace grants are forbidden for registration.", store, StringComparison.Ordinal);
        Assert.Contains("MemoryAccessPermissions.Review", store, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryAccessPermissions.Admin", store, StringComparison.Ordinal);
        Assert.Contains("AccessAuditActionTypes.ProjectRegistration", store, StringComparison.Ordinal);

        Assert.Contains("IAdminProjectRegistrationStore, PostgresAdminProjectRegistrationStore", registration, StringComparison.Ordinal);
        Assert.Contains("MapMemorySystemAdminProjectRegistrationEndpoints", program, StringComparison.Ordinal);
        Assert.Contains("ProjectRegistration = \"project_registration\"", auditActions, StringComparison.Ordinal);
        Assert.Contains("project_registration", migration, StringComparison.Ordinal);
        Assert.Contains("'planned', 'active'", migration, StringComparison.Ordinal);

        Assert.Contains("| REG-02 | P0 | Done | Developer + Security Professional | First Project Registration API Contract |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| REG-02 | P0 | Done | Add first project registration API contract. |", backlog, StringComparison.Ordinal);
        Assert.Contains("## REG-02 API Contract", registrationPlan, StringComparison.Ordinal);
        Assert.Contains("Idempotency-Key", registrationPlan, StringComparison.Ordinal);
        Assert.Contains("payload-safe audit event", registrationPlan, StringComparison.Ordinal);
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
