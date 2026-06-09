using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MemorySystem.Application.AccessAuditing;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminProjectRoleDefinitionManagementTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OtherPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid TargetPrincipalId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid RoleAssignmentId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid NamespaceGrantId = Guid.Parse("77777777-7777-4777-8777-777777777777");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_project_role_definitions_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm08_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync($"/api/admin/projects/{ProjectId:D}/role-definitions");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Project_role_definition_management_reads_writes_guards_and_audits()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm08_roles_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var listResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectId:D}/role-definitions"));
            using var listPayload = await ReadJsonAsync(listResponse);

            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            Assert.Equal("OPM-08", listPayload.RootElement.GetProperty("contractId").GetString());
            Assert.True(listPayload.RootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(listPayload.RootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
            Assert.Equal(ProjectId, listPayload.RootElement.GetProperty("project").GetProperty("projectId").GetGuid());
            Assert.Equal(1, listPayload.RootElement.GetProperty("activeCount").GetInt32());
            Assert.Equal(0, listPayload.RootElement.GetProperty("disabledCount").GetInt32());

            var researchLead = FindRole(listPayload, "research_lead");
            Assert.Equal("Research Lead", researchLead.GetProperty("displayName").GetString());
            Assert.Equal("knowledge_steward", researchLead.GetProperty("templateRoleId").GetString());
            Assert.Equal(1, researchLead.GetProperty("assignmentCount").GetInt32());
            Assert.Equal(1, researchLead.GetProperty("roleGrantCount").GetInt32());

            using var createResponse = await client.SendAsync(CreateAuthenticatedPutRequest(
                $"/api/admin/projects/{ProjectId:D}/role-definitions/delivery_lead",
                new
                {
                    roleId = "delivery_lead",
                    displayName = "Delivery Lead",
                    description = "Coordinates delivery readiness.",
                    templateRoleId = "developer",
                    status = "active",
                    reason = "OPM-08 creates a delivery project role.",
                    auditEvidenceId = "opm08-delivery-role-001"
                }));
            var createBody = await createResponse.Content.ReadAsStringAsync();
            using var createPayload = JsonDocument.Parse(createBody);

            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
            Assert.Equal("OPM-08", createPayload.RootElement.GetProperty("contractId").GetString());
            Assert.Equal("upserted", createPayload.RootElement.GetProperty("status").GetString());
            Assert.Equal("delivery_lead", createPayload.RootElement.GetProperty("role").GetProperty("roleId").GetString());
            Assert.Equal("developer", createPayload.RootElement.GetProperty("role").GetProperty("templateRoleId").GetString());
            Assert.Equal("project_role_definition_change", createPayload.RootElement.GetProperty("auditEvidence").GetProperty("actionType").GetString());
            Assert.Equal("project_role_definition", createPayload.RootElement.GetProperty("auditEvidence").GetProperty("resourceType").GetString());
            Assert.DoesNotContain("OPM-08 creates a delivery project role.", createBody, StringComparison.Ordinal);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Equal(
                ("upserted", "OPM-08", "active", null, "developer", "0", "0", "opm08-delivery-role-001", "OPM-08 creates a delivery project role."),
                await ReadAuditMetadataAsync(dataSource, "delivery_lead"));

            using var defaultTemplateResponse = await client.SendAsync(CreateAuthenticatedPutRequest(
                $"/api/admin/projects/{ProjectId:D}/role-definitions/developer",
                new
                {
                    roleId = "developer",
                    displayName = "Developer",
                    description = "Invalid template overwrite.",
                    templateRoleId = (string?)null,
                    status = "active",
                    reason = "Invalid template overwrite.",
                    auditEvidenceId = "opm08-default-template"
                }));
            var defaultTemplateBody = await defaultTemplateResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, defaultTemplateResponse.StatusCode);
            Assert.Contains("Default template roles are not project-defined roles", defaultTemplateBody, StringComparison.OrdinalIgnoreCase);

            using var guardedDisableResponse = await client.SendAsync(CreateAuthenticatedPutRequest(
                $"/api/admin/projects/{ProjectId:D}/role-definitions/research_lead",
                new
                {
                    roleId = "research_lead",
                    displayName = "Research Lead",
                    description = "Owns research memory synthesis.",
                    templateRoleId = "knowledge_steward",
                    status = "disabled",
                    reason = "Attempt disable with dependencies.",
                    auditEvidenceId = "opm08-research-disable-blocked"
                }));
            var guardedDisableBody = await guardedDisableResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, guardedDisableResponse.StatusCode);
            Assert.Contains("role assignments and role-targeted grants are removed", guardedDisableBody, StringComparison.OrdinalIgnoreCase);

            using var disableResponse = await client.SendAsync(CreateAuthenticatedPutRequest(
                $"/api/admin/projects/{ProjectId:D}/role-definitions/delivery_lead",
                new
                {
                    roleId = "delivery_lead",
                    displayName = "Delivery Lead",
                    description = "Coordinates delivery readiness.",
                    templateRoleId = "developer",
                    status = "disabled",
                    reason = "OPM-08 disables an unused delivery project role.",
                    auditEvidenceId = "opm08-delivery-role-disable-001"
                }));
            using var disablePayload = await ReadJsonAsync(disableResponse);

            Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
            Assert.Equal("disabled", disablePayload.RootElement.GetProperty("status").GetString());
            Assert.Equal("disabled", disablePayload.RootElement.GetProperty("role").GetProperty("status").GetString());

            using var refreshedResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectId:D}/role-definitions"));
            using var refreshedPayload = await ReadJsonAsync(refreshedResponse);
            Assert.Equal(1, refreshedPayload.RootElement.GetProperty("activeCount").GetInt32());
            Assert.Equal(1, refreshedPayload.RootElement.GetProperty("disabledCount").GetInt32());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Project_role_definition_management_rejects_forbidden_operator()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm08_forbidden_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString, OtherPrincipalId);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectId:D}/role-definitions"));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("Actor must", body, StringComparison.Ordinal);
            Assert.DoesNotContain("parent organization", body, StringComparison.OrdinalIgnoreCase);
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
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, OtherPrincipalId, displayName: "No Access");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, TargetPrincipalId, displayName: "Research Lead");
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            OrgId,
            ProjectId,
            organizationName: "OPM-08 Organization",
            projectName: "OPM-08 Project",
            projectStatus: "active");
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, TargetPrincipalId, "contributor");
        await InsertProjectRoleDefinitionAsync(connectionString);
        await InsertRoleAssignmentAsync(connectionString);
        await InsertNamespaceGrantAsync(connectionString);
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
                'Owns research memory synthesis.',
                'knowledge_steward',
                'active'
            );
            """,
            connection);
        command.Parameters.AddWithValue("project_id", ProjectId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertRoleAssignmentAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO role_assignments (id, principal_id, role_id, scope_type, scope_id)
            VALUES (@assignment_id, @principal_id, 'research_lead', 'project', @project_id);
            """,
            connection);
        command.Parameters.AddWithValue("assignment_id", RoleAssignmentId);
        command.Parameters.AddWithValue("principal_id", TargetPrincipalId);
        command.Parameters.AddWithValue("project_id", ProjectId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertNamespaceGrantAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_access_grants (
                id,
                principal_id,
                role_id,
                namespace_prefix,
                permission
            )
            VALUES (
                @grant_id,
                NULL,
                'research_lead',
                @namespace_prefix,
                'read'
            );
            """,
            connection);
        command.Parameters.AddWithValue("grant_id", NamespaceGrantId);
        command.Parameters.AddWithValue("namespace_prefix", $"/project/{ProjectId:D}/facts");

        await command.ExecuteNonQueryAsync();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string postgresConnectionString,
        Guid? principalId = null)
    {
        return MemorySystemApiTestFactory.Create(
            postgresConnectionString,
            TestApiKey,
            (principalId ?? ActorPrincipalId).ToString("D"));
    }

    private static HttpRequestMessage CreateAuthenticatedGetRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Api-Key", TestApiKey);
        return request;
    }

    private static HttpRequestMessage CreateAuthenticatedPutRequest(string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Api-Key", TestApiKey);
        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    private static JsonElement FindRole(JsonDocument payload, string roleId)
    {
        return payload.RootElement.GetProperty("roles")
            .EnumerateArray()
            .First(role => role.GetProperty("roleId").GetString() == roleId);
    }

    private static async Task<(
        string? Operation,
        string? ContractId,
        string? RoleStatus,
        string? PreviousRoleStatus,
        string? TemplateRoleId,
        string? RoleAssignmentCount,
        string? RoleGrantCount,
        string? AuditEvidenceId,
        string? Reason)> ReadAuditMetadataAsync(
            NpgsqlDataSource dataSource,
            string roleId)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT
                audit_metadata->>'operation',
                audit_metadata->>'contractId',
                audit_metadata->>'roleStatus',
                audit_metadata->>'previousRoleStatus',
                audit_metadata->>'templateRoleId',
                audit_metadata->>'roleAssignmentCount',
                audit_metadata->>'roleGrantCount',
                audit_metadata->>'auditEvidenceId',
                audit_metadata->>'reason'
            FROM access_audit_events
            WHERE actor_principal_id = @actor_principal_id
                AND action_type = @action_type
                AND resource_type = 'project_role_definition'
                AND resource_id = @resource_id
            ORDER BY occurred_at DESC, id DESC
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("actor_principal_id", ActorPrincipalId);
        command.Parameters.AddWithValue("action_type", AccessAuditActionTypes.ProjectRoleDefinitionChange);
        command.Parameters.AddWithValue("resource_id", $"{ProjectId:D}:{roleId}");

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Project role definition audit metadata was not returned.");
        }

        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8));
    }
}
