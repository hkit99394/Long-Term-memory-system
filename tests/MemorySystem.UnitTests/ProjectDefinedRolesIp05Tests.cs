namespace MemorySystem.UnitTests;

public sealed class ProjectDefinedRolesIp05Tests
{
    [Fact]
    public void Ip05_project_defined_roles_are_documented_migrated_and_exposed()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "project-defined-roles-ip05.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var migration = File.ReadAllText(Path.Combine(root, "migrations", "030_project_role_definitions.sql"));
        var requests = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAccessManagementRequests.cs"));
        var endpoints = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAccessManagementEndpointExtensions.cs"));
        var roleValue = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Domain", "Roles", "MemoryRoleId.cs"));
        var projectRoleStore = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Roles", "PostgresProjectRoleDefinitionStore.cs"));
        var proposalWorkflow = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "MemoryProposals", "MemoryProposalWorkflow.cs"));

        Assert.Contains("| IP-05 | P0 | Done | Product Owner + CTO | Project-Defined Roles |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Project-Defined Roles IP-05", contract, StringComparison.Ordinal);
        Assert.Contains("POST /api/admin/access/project-roles", contract, StringComparison.Ordinal);
        Assert.Contains("project_role_definitions", contract, StringComparison.Ordinal);
        Assert.Contains("project_role_definition_change", contract, StringComparison.Ordinal);
        Assert.Contains("default role templates", contract, StringComparison.Ordinal);
        Assert.Contains("active project role definition", contract, StringComparison.Ordinal);
        Assert.Contains("Project admins can also create grants for project role-lens namespaces", contract, StringComparison.Ordinal);

        Assert.Contains("[Project-Defined Roles IP-05](project-defined-roles-ip05.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("project-defined-roles-ip05.md", folderStructure, StringComparison.Ordinal);

        Assert.Contains("CREATE TABLE IF NOT EXISTS project_role_definitions", migration, StringComparison.Ordinal);
        Assert.Contains("^[a-z][a-z0-9_-]{0,63}$", migration, StringComparison.Ordinal);
        Assert.Contains("project_role_definition_change", migration, StringComparison.Ordinal);

        Assert.Contains("AdminProjectRoleDefinitionRequest", requests, StringComparison.Ordinal);
        Assert.Contains("/api/admin/access/project-roles", endpoints, StringComparison.Ordinal);
        Assert.Contains("ShouldAuthorizeProjectRoleNamespaceGrantByScopeOnly", endpoints, StringComparison.Ordinal);
        Assert.Contains("TryNormalizeIdentifier", roleValue, StringComparison.Ordinal);
        Assert.Contains("DefaultTemplates => All", roleValue, StringComparison.Ordinal);
        Assert.Contains("IsActiveProjectRoleAsync", projectRoleStore, StringComparison.Ordinal);
        Assert.Contains("Role-lens proposals require a default role template or active project role definition.", proposalWorkflow, StringComparison.Ordinal);
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
