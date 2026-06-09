using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminOrganizationProjectManagementTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ProjectAdminPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OtherPrincipalId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid MemberPrincipalId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid OrgId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid OtherOrgId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid ProjectAlphaId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid ProjectBetaId = Guid.Parse("88888888-8888-4888-8888-888888888888");
    private static readonly Guid HiddenProjectId = Guid.Parse("99999999-9999-4999-8999-999999999999");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_organization_project_management_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm01_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/api/admin/organizations");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_organization_project_management_lists_payload_safe_visible_counts()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm01_list_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var organizationsResponse = await client.SendAsync(CreateAuthenticatedGetRequest("/api/admin/organizations"));
            using var organizationsPayload = await ReadJsonAsync(organizationsResponse);

            Assert.Equal(HttpStatusCode.OK, organizationsResponse.StatusCode);
            Assert.Equal("OPM-01", organizationsPayload.RootElement.GetProperty("contractId").GetString());
            Assert.True(organizationsPayload.RootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(organizationsPayload.RootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());

            var organizations = organizationsPayload.RootElement.GetProperty("organizations");
            Assert.Equal(1, organizations.GetArrayLength());
            var organization = organizations[0];
            Assert.Equal(OrgId, organization.GetProperty("organizationId").GetGuid());
            Assert.Equal("owner", organization.GetProperty("actorAccessLevel").GetString());
            Assert.Equal(2, organization.GetProperty("projectCount").GetInt32());
            Assert.Equal(2, organization.GetProperty("organizationMembershipCount").GetInt32());
            Assert.Equal(2, organization.GetProperty("projectMembershipCount").GetInt32());
            Assert.Equal(1, organization.GetProperty("roleAssignmentCount").GetInt32());
            Assert.Equal(1, organization.GetProperty("namespaceGrantCount").GetInt32());
            Assert.Equal(1, organization.GetProperty("projectStatusCounts").GetProperty("active").GetInt32());
            Assert.Equal(1, organization.GetProperty("projectStatusCounts").GetProperty("archived").GetInt32());

            using var firstPageResponse = await client.SendAsync(CreateAuthenticatedGetRequest("/api/admin/projects?orgId=" + OrgId + "&limit=1"));
            using var firstPagePayload = await ReadJsonAsync(firstPageResponse);
            Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
            Assert.Equal(1, firstPagePayload.RootElement.GetProperty("projects").GetArrayLength());
            Assert.Equal("1", firstPagePayload.RootElement.GetProperty("nextCursor").GetString());

            using var secondPageResponse = await client.SendAsync(CreateAuthenticatedGetRequest("/api/admin/projects?orgId=" + OrgId + "&limit=1&cursor=1"));
            using var secondPagePayload = await ReadJsonAsync(secondPageResponse);
            Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
            Assert.Equal(1, secondPagePayload.RootElement.GetProperty("projects").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, secondPagePayload.RootElement.GetProperty("nextCursor").ValueKind);

            using var activeProjectsResponse = await client.SendAsync(CreateAuthenticatedGetRequest("/api/admin/projects?status=active"));
            using var activeProjectsPayload = await ReadJsonAsync(activeProjectsResponse);
            var activeProjects = activeProjectsPayload.RootElement.GetProperty("projects");
            Assert.Equal(1, activeProjects.GetArrayLength());
            Assert.Equal(ProjectAlphaId, activeProjects[0].GetProperty("projectId").GetGuid());
            Assert.Equal("org_owner", activeProjects[0].GetProperty("actorAccessLevel").GetString());
            Assert.Equal(1, activeProjects[0].GetProperty("projectMembershipCount").GetInt32());
            Assert.Equal(1, activeProjects[0].GetProperty("roleDefinitionCount").GetInt32());
            Assert.Equal(1, activeProjects[0].GetProperty("activeRoleDefinitionCount").GetInt32());
            Assert.Equal(1, activeProjects[0].GetProperty("roleAssignmentCount").GetInt32());
            Assert.Equal(1, activeProjects[0].GetProperty("namespaceGrantCount").GetInt32());

            using var detailResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectAlphaId:D}"));
            using var detailPayload = await ReadJsonAsync(detailResponse);
            Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
            Assert.Equal(ProjectAlphaId, detailPayload.RootElement.GetProperty("project").GetProperty("projectId").GetGuid());
            var evidence = detailPayload.RootElement.GetProperty("latestRegistrationEvidence");
            Assert.True(evidence.GetProperty("payloadSafe").GetBoolean());
            Assert.False(evidence.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
            Assert.Equal(2, evidence.GetProperty("sourceDocumentCount").GetInt32());
            Assert.Equal(100, evidence.GetProperty("sourceHashCoveragePercent").GetInt32());

            using var hiddenOrganizationResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/organizations/{OtherOrgId:D}"));
            Assert.Equal(HttpStatusCode.NotFound, hiddenOrganizationResponse.StatusCode);

            using var hiddenProjectResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{HiddenProjectId:D}"));
            Assert.Equal(HttpStatusCode.NotFound, hiddenProjectResponse.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_project_management_allows_direct_project_admin_without_org_directory_leak()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm01_project_admin_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString, ProjectAdminPrincipalId);
            using var client = factory.CreateClient();

            using var organizationsResponse = await client.SendAsync(CreateAuthenticatedGetRequest("/api/admin/organizations"));
            using var organizationsPayload = await ReadJsonAsync(organizationsResponse);
            Assert.Equal(HttpStatusCode.OK, organizationsResponse.StatusCode);
            Assert.Empty(organizationsPayload.RootElement.GetProperty("organizations").EnumerateArray());

            using var projectsResponse = await client.SendAsync(CreateAuthenticatedGetRequest("/api/admin/projects"));
            using var projectsPayload = await ReadJsonAsync(projectsResponse);
            Assert.Equal(HttpStatusCode.OK, projectsResponse.StatusCode);
            var projects = projectsPayload.RootElement.GetProperty("projects");
            Assert.Equal(1, projects.GetArrayLength());
            Assert.Equal(ProjectAlphaId, projects[0].GetProperty("projectId").GetGuid());
            Assert.Equal("project_admin", projects[0].GetProperty("actorAccessLevel").GetString());

            using var hiddenProjectResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectBetaId:D}"));
            Assert.Equal(HttpStatusCode.NotFound, hiddenProjectResponse.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_project_management_rejects_invalid_filters()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm01_filter_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var badStatus = await client.SendAsync(CreateAuthenticatedGetRequest("/api/admin/projects?status=paused"));
            Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);

            using var badCursor = await client.SendAsync(CreateAuthenticatedGetRequest("/api/admin/projects?cursor=not-a-cursor"));
            Assert.Equal(HttpStatusCode.BadRequest, badCursor.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task PrepareFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ActorPrincipalId, displayName: "Organization Owner");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ProjectAdminPrincipalId, displayName: "Project Admin");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, OtherPrincipalId, displayName: "Other Owner");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, MemberPrincipalId, displayName: "Project Member");

        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            OrgId,
            ProjectAlphaId,
            organizationName: "Visible Organization",
            projectName: "Alpha Project",
            projectStatus: "active");
        await InsertProjectAsync(connectionString, OrgId, ProjectBetaId, "Beta Project", "archived");
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            OtherOrgId,
            HiddenProjectId,
            organizationName: "Hidden Organization",
            projectName: "Hidden Project",
            projectStatus: "active");

        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, MemberPrincipalId, "reader");
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OtherOrgId, OtherPrincipalId, "owner");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectAlphaId, ProjectAdminPrincipalId, "admin");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectBetaId, MemberPrincipalId, "reader");
        await ApiDatabaseTestSupport.InsertRoleAssignmentAsync(
            connectionString,
            ProjectAdminPrincipalId,
            "developer",
            scopeType: "project",
            scopeId: ProjectAlphaId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAlphaId:D}/goals",
            "read",
            roleId: "developer");

        await InsertProjectRoleDefinitionAsync(connectionString);
        await InsertProjectRegistrationAuditEventAsync(connectionString);
    }

    private static async Task InsertProjectAsync(
        string connectionString,
        Guid orgId,
        Guid projectId,
        string projectName,
        string projectStatus)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO projects (id, org_id, name, status)
            VALUES (@project_id, @org_id, @project_name, @project_status);
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("project_name", projectName);
        command.Parameters.AddWithValue("project_status", projectStatus);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertProjectRoleDefinitionAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO project_role_definitions (
                project_id,
                role_id,
                display_name,
                description,
                template_role_id,
                status
            )
            VALUES (
                @project_id,
                'research_lead',
                'Research Lead',
                'Owns research interpretation.',
                'developer',
                'active'
            );
            """,
            connection);
        command.Parameters.AddWithValue("project_id", ProjectAlphaId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertProjectRegistrationAuditEventAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO access_audit_events (
                id,
                action_type,
                outcome,
                actor_principal_id,
                scope_type,
                scope_id,
                resource_type,
                resource_id,
                request_method,
                request_path,
                audit_metadata
            )
            VALUES (
                @id,
                'project_registration',
                'succeeded',
                @actor_principal_id,
                'project',
                @project_id_text,
                'project_registration',
                @project_id_text,
                'POST',
                '/api/admin/projects/register',
                @audit_metadata
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
        command.Parameters.AddWithValue("actor_principal_id", ActorPrincipalId);
        command.Parameters.AddWithValue("project_id_text", ProjectAlphaId.ToString("D"));
        command.Parameters.Add("audit_metadata", NpgsqlDbType.Jsonb).Value =
            """
            {
              "contractId": "REG-02",
              "idempotencyRecordId": "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
              "registrationRequestHash": "sha256:opm01",
              "organizationId": "55555555-5555-4555-8555-555555555555",
              "projectId": "77777777-7777-4777-8777-777777777777",
              "projectStatus": "active",
              "roleDefinitionCount": "1",
              "ownerAssignmentCount": "1",
              "namespaceGrantCount": "1",
              "sourceDocumentCount": "2",
              "sourceHashCoveragePercent": "100",
              "accessPreviewReportId": "preview-opm01",
              "auditExportId": "audit-export-opm01"
            }
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string postgresConnectionString,
        Guid? targetActorPrincipalId = null)
    {
        return MemorySystemApiTestFactory.Create(
            postgresConnectionString,
            TestApiKey,
            (targetActorPrincipalId ?? ActorPrincipalId).ToString());
    }

    private static HttpRequestMessage CreateAuthenticatedGetRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Api-Key", TestApiKey);

        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }
}
