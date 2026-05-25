using MemorySystem.Application.Access;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.Access;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class MemoryAccessReferenceStoreTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task HasNamespaceGrantAsync_treats_grant_prefix_as_literal_text()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_grant_literal_prefix_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                "/session/session%1/instructions",
                MemoryAccessPermissions.Read,
                principalId: PrincipalId);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresMemoryAccessReferenceStore(dataSource);

            Assert.True(await store.HasNamespaceGrantAsync(
                PrincipalId,
                new MemoryScopeResolution("session", "session%1"),
                MemoryAccessPermissions.Read,
                "/session/session%1/instructions/private"));

            Assert.False(await store.HasNamespaceGrantAsync(
                PrincipalId,
                new MemoryScopeResolution("session", "sessionXYZ1"),
                MemoryAccessPermissions.Read,
                "/session/sessionXYZ1/instructions/private"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task FindProjectAccessLevelAsync_ignores_inactive_projects()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_inactive_project_access_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);
            await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
                databaseConnectionString,
                OrgId,
                ProjectId,
                projectStatus: "archived");
            await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
                databaseConnectionString,
                ProjectId,
                PrincipalId,
                "admin");

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresMemoryAccessReferenceStore(dataSource);

            Assert.Null(await store.FindProjectAccessLevelAsync(PrincipalId, ProjectId));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Role_scope_checks_do_not_accept_project_role_assignments()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_scope_assignment_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);
            await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(databaseConnectionString, OrgId, ProjectId);
            await ApiDatabaseTestSupport.InsertRoleAssignmentAsync(
                databaseConnectionString,
                PrincipalId,
                "cto",
                "project",
                ProjectId);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                "/role/cto/shared",
                MemoryAccessPermissions.Read,
                roleId: "cto");

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresMemoryAccessReferenceStore(dataSource);
            var roleScope = new MemoryScopeResolution("role", "cto", RoleId: "cto", ScopeRoleId: "cto");

            Assert.False(await store.HasRoleAssignmentAsync(PrincipalId, "cto", roleScope));
            Assert.False(await store.HasNamespaceGrantAsync(
                PrincipalId,
                roleScope,
                MemoryAccessPermissions.Read,
                "/role/cto/shared/strategy"));

            await ApiDatabaseTestSupport.InsertRoleAssignmentAsync(
                databaseConnectionString,
                PrincipalId,
                "cto");

            Assert.True(await store.HasRoleAssignmentAsync(PrincipalId, "cto", roleScope));
            Assert.True(await store.HasNamespaceGrantAsync(
                PrincipalId,
                roleScope,
                MemoryAccessPermissions.Read,
                "/role/cto/shared/strategy"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
