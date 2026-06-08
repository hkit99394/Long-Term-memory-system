using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Admin;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminAccessManagementTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid TargetPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OrgId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_access_management_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_access_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "/api/admin/access/project-memberships",
                new
                {
                    projectId = ProjectId,
                    principalId = TargetPrincipalId,
                    accessLevel = "reader"
                });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_access_management_writes_memberships_roles_grants_preview_and_audit_records()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_access_management_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareAccessFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var orgResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/organization-memberships",
                new
                {
                    orgId = OrgId,
                    principalId = TargetPrincipalId,
                    accessLevel = "reader"
                }));
            var orgBody = await orgResponse.Content.ReadAsStringAsync();
            using var orgPayload = JsonDocument.Parse(orgBody);

            Assert.Equal(HttpStatusCode.OK, orgResponse.StatusCode);
            Assert.Equal("reader", orgPayload.RootElement.GetProperty("accessLevel").GetString());

            using var projectResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/project-memberships",
                new
                {
                    projectId = ProjectId,
                    principalId = TargetPrincipalId,
                    accessLevel = "contributor"
                }));
            var projectBody = await projectResponse.Content.ReadAsStringAsync();
            using var projectPayload = JsonDocument.Parse(projectBody);

            Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
            Assert.Equal("contributor", projectPayload.RootElement.GetProperty("accessLevel").GetString());

            using var roleResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/role-assignments",
                new
                {
                    principalId = TargetPrincipalId,
                    roleId = "cto",
                    scopeType = "project",
                    scopeId = ProjectId
                }));
            var roleBody = await roleResponse.Content.ReadAsStringAsync();
            using var rolePayload = JsonDocument.Parse(roleBody);

            Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);
            Assert.Equal("cto", rolePayload.RootElement.GetProperty("roleId").GetString());
            Assert.Equal("project", rolePayload.RootElement.GetProperty("scopeType").GetString());

            using var grantResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/namespace-grants",
                new
                {
                    principalId = TargetPrincipalId,
                    roleId = (string?)null,
                    namespacePrefix = $"/project/{ProjectId}/decisions",
                    permission = "read",
                    scopeType = "project",
                    scopeId = ProjectId
                }));
            var grantBody = await grantResponse.Content.ReadAsStringAsync();
            using var grantPayload = JsonDocument.Parse(grantBody);

            Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);
            Assert.Equal("read", grantPayload.RootElement.GetProperty("permission").GetString());

            using var previewResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/effective-preview",
                new
                {
                    principalId = TargetPrincipalId,
                    permission = "read",
                    scopeType = "project",
                    scopeId = ProjectId.ToString(),
                    namespacePrefix = $"/project/{ProjectId}/decisions/source"
                }));
            var previewBody = await previewResponse.Content.ReadAsStringAsync();
            using var previewPayload = JsonDocument.Parse(previewBody);

            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
            Assert.True(previewPayload.RootElement.GetProperty("allowed").GetBoolean());
            Assert.Equal("IMemoryAccessAuthorizer", previewPayload.RootElement.GetProperty("evaluatedBy").GetString());

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Equal("reader", await ReadOrganizationAccessLevelAsync(dataSource, TargetPrincipalId));
            Assert.Equal("contributor", await ReadProjectAccessLevelAsync(dataSource, TargetPrincipalId));
            Assert.True(await HasRoleAssignmentAsync(dataSource, TargetPrincipalId, "cto"));
            Assert.True(await HasNamespaceGrantAsync(dataSource, TargetPrincipalId, $"/project/{ProjectId}/decisions", "read"));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.OrganizationMembershipChange));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.ProjectMembershipChange));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.RoleAssignmentChange));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.NamespaceGrantChange));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_access_management_defines_project_roles_before_custom_role_assignments()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_project_roles_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareAccessFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var undefinedRoleResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/role-assignments",
                new
                {
                    principalId = TargetPrincipalId,
                    roleId = "implementation_lead",
                    scopeType = "project",
                    scopeId = ProjectId
                }));
            var undefinedRoleBody = await undefinedRoleResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, undefinedRoleResponse.StatusCode);
            Assert.Contains("active project role definition", undefinedRoleBody, StringComparison.Ordinal);

            using var orgCustomRoleResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/role-assignments",
                new
                {
                    principalId = TargetPrincipalId,
                    roleId = "implementation_lead",
                    scopeType = "org",
                    scopeId = OrgId
                }));
            var orgCustomRoleBody = await orgCustomRoleResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, orgCustomRoleResponse.StatusCode);
            Assert.Contains("not supported for organization scope", orgCustomRoleBody, StringComparison.Ordinal);

            using var projectRoleResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/project-roles",
                new
                {
                    projectId = ProjectId,
                    roleId = "Implementation_Lead",
                    displayName = "Implementation Lead",
                    description = "Owns sequencing, merge readiness, and delivery risk for this project.",
                    templateRoleId = "developer",
                    status = "active"
                }));
            var projectRoleBody = await projectRoleResponse.Content.ReadAsStringAsync();
            using var projectRolePayload = JsonDocument.Parse(projectRoleBody);

            Assert.Equal(HttpStatusCode.OK, projectRoleResponse.StatusCode);
            Assert.Equal("implementation_lead", projectRolePayload.RootElement.GetProperty("roleId").GetString());
            Assert.Equal("Implementation Lead", projectRolePayload.RootElement.GetProperty("displayName").GetString());
            Assert.Equal("developer", projectRolePayload.RootElement.GetProperty("templateRoleId").GetString());
            Assert.Equal("active", projectRolePayload.RootElement.GetProperty("status").GetString());

            using var assignmentResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/role-assignments",
                new
                {
                    principalId = TargetPrincipalId,
                    roleId = "implementation_lead",
                    scopeType = "project",
                    scopeId = ProjectId
                }));
            var assignmentBody = await assignmentResponse.Content.ReadAsStringAsync();
            using var assignmentPayload = JsonDocument.Parse(assignmentBody);

            Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);
            Assert.Equal("implementation_lead", assignmentPayload.RootElement.GetProperty("roleId").GetString());

            using var grantResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/namespace-grants",
                new
                {
                    principalId = (Guid?)null,
                    roleId = "implementation_lead",
                    namespacePrefix = $"/project/{ProjectId}/role/implementation_lead/lens",
                    permission = "read",
                    scopeType = "project",
                    scopeId = ProjectId
                }));
            var grantBody = await grantResponse.Content.ReadAsStringAsync();
            using var grantPayload = JsonDocument.Parse(grantBody);

            Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);
            Assert.Equal("implementation_lead", grantPayload.RootElement.GetProperty("roleId").GetString());
            Assert.Equal("read", grantPayload.RootElement.GetProperty("permission").GetString());

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.True(await HasProjectRoleDefinitionAsync(dataSource, "implementation_lead"));
            Assert.True(await HasRoleAssignmentAsync(dataSource, TargetPrincipalId, "implementation_lead"));
            Assert.True(await HasRoleNamespaceGrantAsync(
                dataSource,
                "implementation_lead",
                $"/project/{ProjectId}/role/implementation_lead/lens",
                "read"));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.ProjectRoleDefinitionChange));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.RoleAssignmentChange));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.NamespaceGrantChange));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_access_management_rejects_case_insensitive_self_admin_escalation()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_access_self_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareAccessFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var projectResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/project-memberships",
                new
                {
                    projectId = ProjectId,
                    principalId = ActorPrincipalId,
                    accessLevel = "Admin"
                }));
            var projectBody = await projectResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, projectResponse.StatusCode);
            Assert.Contains("Operators cannot grant themselves admin access.", projectBody, StringComparison.Ordinal);

            using var orgResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/organization-memberships",
                new
                {
                    orgId = OrgId,
                    principalId = ActorPrincipalId,
                    accessLevel = "Owner"
                }));
            var orgBody = await orgResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, orgResponse.StatusCode);
            Assert.Contains("Operators cannot grant themselves admin or owner access.", orgBody, StringComparison.Ordinal);

            using var grantResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/namespace-grants",
                new
                {
                    principalId = ActorPrincipalId,
                    roleId = (string?)null,
                    namespacePrefix = $"/project/{ProjectId}/plans",
                    permission = "Admin",
                    scopeType = "project",
                    scopeId = ProjectId
                }));
            var grantBody = await grantResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, grantResponse.StatusCode);
            Assert.Contains("Operators cannot grant themselves namespace admin access.", grantBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_access_management_requires_break_glass_evidence_for_namespace_admin()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_access_break_glass_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareAccessFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var missingEvidenceResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/namespace-grants",
                new
                {
                    principalId = TargetPrincipalId,
                    roleId = (string?)null,
                    namespacePrefix = $"/project/{ProjectId}/decisions/break-glass",
                    permission = "admin",
                    scopeType = "project",
                    scopeId = ProjectId
                }));
            var missingEvidenceBody = await missingEvidenceResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, missingEvidenceResponse.StatusCode);
            Assert.Contains("Break-glass evidence is required for namespace admin grants.", missingEvidenceBody, StringComparison.Ordinal);

            using var roleAdminResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/namespace-grants",
                new
                {
                    principalId = (Guid?)null,
                    roleId = "cto",
                    namespacePrefix = $"/project/{ProjectId}/decisions/break-glass",
                    permission = "admin",
                    scopeType = "project",
                    scopeId = ProjectId,
                    breakGlassEvidence = ValidBreakGlassEvidence()
                }));
            var roleAdminBody = await roleAdminResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, roleAdminResponse.StatusCode);
            Assert.Contains("Break-glass namespace admin grants must target exactly one principal id.", roleAdminBody, StringComparison.Ordinal);

            using var projectRootAdminResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/namespace-grants",
                new
                {
                    principalId = TargetPrincipalId,
                    roleId = (string?)null,
                    namespacePrefix = $"/project/{ProjectId}",
                    permission = "admin",
                    scopeType = "project",
                    scopeId = ProjectId,
                    breakGlassEvidence = ValidBreakGlassEvidence()
                }));
            var projectRootAdminBody = await projectRootAdminResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, projectRootAdminResponse.StatusCode);
            Assert.Contains("Project root namespace admin grants must stay absent.", projectRootAdminBody, StringComparison.Ordinal);

            using var grantResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/namespace-grants",
                new
                {
                    principalId = TargetPrincipalId,
                    roleId = (string?)null,
                    namespacePrefix = $"/project/{ProjectId}/decisions/break-glass",
                    permission = "admin",
                    scopeType = "project",
                    scopeId = ProjectId,
                    breakGlassEvidence = ValidBreakGlassEvidence()
                }));
            var grantBody = await grantResponse.Content.ReadAsStringAsync();
            using var grantPayload = JsonDocument.Parse(grantBody);

            Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);
            Assert.Equal("admin", grantPayload.RootElement.GetProperty("permission").GetString());

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.True(await HasNamespaceGrantAsync(dataSource, TargetPrincipalId, $"/project/{ProjectId}/decisions/break-glass", "admin"));
            var auditMetadata = await ReadLatestNamespaceGrantAuditMetadataAsync(dataSource);
            Assert.Equal("true", auditMetadata.BreakGlass);
            Assert.Equal("security_professional", auditMetadata.AcceptedByRole);
            Assert.Equal("2099-12-31", auditMetadata.ReviewDue);
            Assert.Equal("reg04-break-glass-test", auditMetadata.AuditEvidenceId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Admin_access_store_rejects_direct_namespace_admin_without_break_glass_contract()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_access_store_break_glass_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareAccessFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var scope = factory.Services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IAdminAccessManagementStore>();

            var missingEvidence = await Assert.ThrowsAsync<ArgumentException>(() =>
                store.UpsertNamespaceGrantAsync(new AdminNamespaceGrantCommand(
                    ActorPrincipalId,
                    TargetPrincipalId,
                    RoleId: null,
                    ScopeType: "project",
                    ScopeId: ProjectId,
                    NamespacePrefix: $"/project/{ProjectId}/decisions/direct",
                    Permission: "admin",
                    BreakGlassEvidence: null)));
            Assert.Contains("Break-glass evidence is required for namespace admin grants.", missingEvidence.Message, StringComparison.Ordinal);

            var rootNamespace = await Assert.ThrowsAsync<ArgumentException>(() =>
                store.UpsertNamespaceGrantAsync(new AdminNamespaceGrantCommand(
                    ActorPrincipalId,
                    TargetPrincipalId,
                    RoleId: null,
                    ScopeType: "project",
                    ScopeId: ProjectId,
                    NamespacePrefix: $"/project/{ProjectId}",
                    Permission: "admin",
                    BreakGlassEvidence: ValidBreakGlassEvidenceCommand())));
            Assert.Contains("Project root namespace admin grants must stay absent.", rootNamespace.Message, StringComparison.Ordinal);

            var roleTarget = await Assert.ThrowsAsync<ArgumentException>(() =>
                store.UpsertNamespaceGrantAsync(new AdminNamespaceGrantCommand(
                    ActorPrincipalId,
                    PrincipalId: null,
                    RoleId: "cto",
                    ScopeType: "project",
                    ScopeId: ProjectId,
                    NamespacePrefix: $"/project/{ProjectId}/decisions/direct",
                    Permission: "admin",
                    BreakGlassEvidence: ValidBreakGlassEvidenceCommand())));
            Assert.Contains("Break-glass namespace admin grants must target exactly one principal id.", roleTarget.Message, StringComparison.Ordinal);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.False(await HasNamespaceGrantAsync(dataSource, TargetPrincipalId, $"/project/{ProjectId}/decisions/direct", "admin"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_access_effective_preview_rejects_invalid_permission()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_access_preview_invalid_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareAccessFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/effective-preview",
                new
                {
                    principalId = TargetPrincipalId,
                    permission = "delete",
                    scopeType = "project",
                    scopeId = ProjectId.ToString(),
                    namespacePrefix = $"/project/{ProjectId}/decisions"
                }));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Permission is not supported.", body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static object ValidBreakGlassEvidence()
    {
        return new
        {
            ownerRole = "product_owner",
            acceptedByRole = "security_professional",
            reason = "Temporary admin repair for a bounded project namespace.",
            reviewDue = "2099-12-31",
            cleanupAction = "Remove the namespace admin grant after repair.",
            auditEvidenceId = "reg04-break-glass-test"
        };
    }

    private static AdminBreakGlassGrantEvidenceCommand ValidBreakGlassEvidenceCommand()
    {
        return new AdminBreakGlassGrantEvidenceCommand(
            "product_owner",
            "security_professional",
            "Temporary admin repair for a bounded project namespace.",
            DateOnly.Parse("2099-12-31", CultureInfo.InvariantCulture),
            "Remove the namespace admin grant after repair.",
            "reg04-break-glass-test");
    }

    private static async Task PrepareAccessFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ActorPrincipalId, displayName: "Access Admin");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, TargetPrincipalId, displayName: "Managed User");
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgId, ProjectId);
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, ActorPrincipalId, "admin");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectId}/decisions",
            "admin",
            principalId: ActorPrincipalId);
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, ActorPrincipalId.ToString());
    }

    private static HttpRequestMessage CreateAuthenticatedJsonRequest(string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Api-Key", TestApiKey);

        return request;
    }

    private static async Task<string?> ReadOrganizationAccessLevelAsync(
        NpgsqlDataSource dataSource,
        Guid principalId)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT access_level
            FROM organization_memberships
            WHERE org_id = @org_id
                AND principal_id = @principal_id;
            """);
        command.Parameters.AddWithValue("org_id", OrgId);
        command.Parameters.AddWithValue("principal_id", principalId);

        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<string?> ReadProjectAccessLevelAsync(
        NpgsqlDataSource dataSource,
        Guid principalId)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT access_level
            FROM project_memberships
            WHERE project_id = @project_id
                AND principal_id = @principal_id;
            """);
        command.Parameters.AddWithValue("project_id", ProjectId);
        command.Parameters.AddWithValue("principal_id", principalId);

        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<bool> HasRoleAssignmentAsync(
        NpgsqlDataSource dataSource,
        Guid principalId,
        string roleId)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM role_assignments
                WHERE principal_id = @principal_id
                    AND role_id = @role_id
                    AND scope_type = 'project'
                    AND scope_id = @project_id
            );
            """);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("project_id", ProjectId);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<bool> HasNamespaceGrantAsync(
        NpgsqlDataSource dataSource,
        Guid principalId,
        string namespacePrefix,
        string permission)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM memory_access_grants
                WHERE principal_id = @principal_id
                    AND namespace_prefix = @namespace_prefix
                    AND permission = @permission
            );
            """);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("namespace_prefix", namespacePrefix);
        command.Parameters.AddWithValue("permission", permission);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<(string? BreakGlass, string? AcceptedByRole, string? ReviewDue, string? AuditEvidenceId)> ReadLatestNamespaceGrantAuditMetadataAsync(
        NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT
                audit_metadata->>'breakGlass',
                audit_metadata->>'breakGlassAcceptedByRole',
                audit_metadata->>'breakGlassReviewDue',
                audit_metadata->>'breakGlassAuditEvidenceId'
            FROM access_audit_events
            WHERE actor_principal_id = @actor_principal_id
                AND action_type = @action_type
            ORDER BY occurred_at DESC
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("actor_principal_id", ActorPrincipalId);
        command.Parameters.AddWithValue("action_type", AccessAuditActionTypes.NamespaceGrantChange);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Namespace-grant audit metadata was not returned.");
        }

        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private static async Task<bool> HasProjectRoleDefinitionAsync(
        NpgsqlDataSource dataSource,
        string roleId)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM project_role_definitions
                WHERE project_id = @project_id
                    AND role_id = @role_id
                    AND template_role_id = 'developer'
                    AND status = 'active'
            );
            """);
        command.Parameters.AddWithValue("project_id", ProjectId);
        command.Parameters.AddWithValue("role_id", roleId);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<bool> HasRoleNamespaceGrantAsync(
        NpgsqlDataSource dataSource,
        string roleId,
        string namespacePrefix,
        string permission)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM memory_access_grants
                WHERE role_id = @role_id
                    AND principal_id IS NULL
                    AND namespace_prefix = @namespace_prefix
                    AND permission = @permission
            );
            """);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("namespace_prefix", namespacePrefix);
        command.Parameters.AddWithValue("permission", permission);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<long> CountAuditEventsAsync(
        NpgsqlDataSource dataSource,
        string actionType)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT count(*)
            FROM access_audit_events
            WHERE actor_principal_id = @actor_principal_id
                AND action_type = @action_type;
            """);
        command.Parameters.AddWithValue("actor_principal_id", ActorPrincipalId);
        command.Parameters.AddWithValue("action_type", actionType);

        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Audit count was not returned."));
    }
}
