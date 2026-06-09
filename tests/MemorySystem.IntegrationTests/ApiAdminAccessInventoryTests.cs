using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MemorySystem.Application.AccessAuditing;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminAccessInventoryTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid TargetPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid DisabledPrincipalId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid RoleAssignmentId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid NamespaceGrantId = Guid.Parse("77777777-7777-4777-8777-777777777777");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_access_inventory_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm04_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync($"/api/admin/projects/{ProjectId:D}/access-inventory");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Project_access_inventory_lists_stale_access_and_revokes_with_payload_safe_audit()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm04_inventory_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var inventoryResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectId:D}/access-inventory"));
            using var inventoryPayload = await ReadJsonAsync(inventoryResponse);

            Assert.Equal(HttpStatusCode.OK, inventoryResponse.StatusCode);
            Assert.Equal("OPM-04", inventoryPayload.RootElement.GetProperty("contractId").GetString());
            Assert.True(inventoryPayload.RootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(inventoryPayload.RootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
            Assert.Equal("project", inventoryPayload.RootElement.GetProperty("scope").GetProperty("scopeType").GetString());
            Assert.Equal(1, inventoryPayload.RootElement.GetProperty("counts").GetProperty("organizationMemberships").GetInt32());
            Assert.Equal(2, inventoryPayload.RootElement.GetProperty("counts").GetProperty("projectMemberships").GetInt32());
            Assert.Equal(1, inventoryPayload.RootElement.GetProperty("counts").GetProperty("roleAssignments").GetInt32());
            Assert.Equal(1, inventoryPayload.RootElement.GetProperty("counts").GetProperty("namespaceGrants").GetInt32());
            Assert.Equal(1, inventoryPayload.RootElement.GetProperty("staleAccessPrompts").GetArrayLength());
            Assert.Contains(
                "principal status is disabled",
                inventoryPayload.RootElement.GetProperty("staleAccessPrompts")[0].GetProperty("prompt").GetString(),
                StringComparison.Ordinal);

            using var organizationInventoryResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/organizations/{OrgId:D}/access-inventory"));
            using var organizationInventoryPayload = await ReadJsonAsync(organizationInventoryResponse);
            Assert.Equal(HttpStatusCode.OK, organizationInventoryResponse.StatusCode);
            Assert.Equal("org", organizationInventoryPayload.RootElement.GetProperty("scope").GetProperty("scopeType").GetString());
            Assert.Equal(1, organizationInventoryPayload.RootElement.GetProperty("counts").GetProperty("organizationMemberships").GetInt32());
            Assert.Equal(2, organizationInventoryPayload.RootElement.GetProperty("counts").GetProperty("projectMemberships").GetInt32());
            Assert.Equal(1, organizationInventoryPayload.RootElement.GetProperty("counts").GetProperty("roleAssignments").GetInt32());
            Assert.Equal(1, organizationInventoryPayload.RootElement.GetProperty("counts").GetProperty("namespaceGrants").GetInt32());

            using var roleRevocationResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/revocations",
                new
                {
                    scopeType = "project",
                    scopeId = ProjectId,
                    accessRecordType = "role_assignment",
                    accessRecordId = RoleAssignmentId,
                    principalId = (Guid?)null,
                    reason = "OPM-04 removes stale role assignment during access review.",
                    auditEvidenceId = "opm04-role-revoke-001"
                }));
            using var roleRevocationPayload = await ReadJsonAsync(roleRevocationResponse);

            Assert.Equal(HttpStatusCode.OK, roleRevocationResponse.StatusCode);
            Assert.Equal("OPM-04", roleRevocationPayload.RootElement.GetProperty("contractId").GetString());
            Assert.Equal("revoked", roleRevocationPayload.RootElement.GetProperty("status").GetString());
            Assert.Equal("role_assignment", roleRevocationPayload.RootElement.GetProperty("revokedAccess").GetProperty("accessRecordType").GetString());
            Assert.Equal("role_assignment_change", roleRevocationPayload.RootElement.GetProperty("auditEvidence").GetProperty("actionType").GetString());
            Assert.True(roleRevocationPayload.RootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(roleRevocationPayload.RootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());

            using var grantRevocationResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/revocations",
                new
                {
                    scopeType = "project",
                    scopeId = ProjectId,
                    accessRecordType = "namespace_grant",
                    accessRecordId = NamespaceGrantId,
                    principalId = (Guid?)null,
                    reason = "OPM-04 removes stale namespace grant during access review.",
                    auditEvidenceId = "opm04-grant-revoke-001"
                }));
            using var grantRevocationPayload = await ReadJsonAsync(grantRevocationResponse);

            Assert.Equal(HttpStatusCode.OK, grantRevocationResponse.StatusCode);
            Assert.Equal("namespace_grant", grantRevocationPayload.RootElement.GetProperty("revokedAccess").GetProperty("accessRecordType").GetString());
            Assert.Equal("namespace_grant_change", grantRevocationPayload.RootElement.GetProperty("auditEvidence").GetProperty("actionType").GetString());

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.False(await HasRoleAssignmentAsync(dataSource));
            Assert.False(await HasNamespaceGrantAsync(dataSource));
            Assert.Equal(
                ("revoked", "OPM-04", "opm04-role-revoke-001"),
                await ReadAuditMetadataAsync(dataSource, AccessAuditActionTypes.RoleAssignmentChange, RoleAssignmentId.ToString("D")));
            Assert.Equal(
                ("revoked", "OPM-04", "opm04-grant-revoke-001"),
                await ReadAuditMetadataAsync(dataSource, AccessAuditActionTypes.NamespaceGrantChange, NamespaceGrantId.ToString("D")));

            using var refreshedInventoryResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectId:D}/access-inventory"));
            using var refreshedInventoryPayload = await ReadJsonAsync(refreshedInventoryResponse);
            Assert.Equal(HttpStatusCode.OK, refreshedInventoryResponse.StatusCode);
            Assert.Equal(0, refreshedInventoryPayload.RootElement.GetProperty("counts").GetProperty("roleAssignments").GetInt32());
            Assert.Equal(0, refreshedInventoryPayload.RootElement.GetProperty("counts").GetProperty("namespaceGrants").GetInt32());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Access_revocation_rejects_self_revocation()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm04_self_revoke_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/revocations",
                new
                {
                    scopeType = "org",
                    scopeId = OrgId,
                    accessRecordType = "organization_membership",
                    accessRecordId = (Guid?)null,
                    principalId = ActorPrincipalId,
                    reason = "Attempted self revoke.",
                    auditEvidenceId = "opm04-self-revoke"
                }));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("cannot revoke their own access record", body, StringComparison.OrdinalIgnoreCase);
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
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, TargetPrincipalId, displayName: "Managed User");
        await InsertPrincipalAsync(connectionString, DisabledPrincipalId, "Disabled User", "disabled");
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            OrgId,
            ProjectId,
            organizationName: "OPM-04 Organization",
            projectName: "OPM-04 Project",
            projectStatus: "active");
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, TargetPrincipalId, "contributor");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, DisabledPrincipalId, "reader");
        await InsertRoleAssignmentAsync(connectionString);
        await InsertNamespaceGrantAsync(connectionString);
    }

    private static async Task InsertPrincipalAsync(
        string connectionString,
        Guid principalId,
        string displayName,
        string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO principals (id, principal_type, display_name, status)
            VALUES (@principal_id, 'human', @display_name, @status);
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("display_name", displayName);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertRoleAssignmentAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO role_assignments (id, principal_id, role_id, scope_type, scope_id)
            VALUES (@assignment_id, @principal_id, 'developer', 'project', @project_id);
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
                @principal_id,
                NULL,
                @namespace_prefix,
                'read'
            );
            """,
            connection);
        command.Parameters.AddWithValue("grant_id", NamespaceGrantId);
        command.Parameters.AddWithValue("principal_id", TargetPrincipalId);
        command.Parameters.AddWithValue("namespace_prefix", $"/project/{ProjectId:D}/decisions");

        await command.ExecuteNonQueryAsync();
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, ActorPrincipalId.ToString("D"));
    }

    private static HttpRequestMessage CreateAuthenticatedGetRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Api-Key", TestApiKey);
        return request;
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

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    private static async Task<bool> HasRoleAssignmentAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM role_assignments
                WHERE id = @assignment_id
            );
            """);
        command.Parameters.AddWithValue("assignment_id", RoleAssignmentId);
        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<bool> HasNamespaceGrantAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM memory_access_grants
                WHERE id = @grant_id
            );
            """);
        command.Parameters.AddWithValue("grant_id", NamespaceGrantId);
        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<(string? Operation, string? ContractId, string? AuditEvidenceId)> ReadAuditMetadataAsync(
        NpgsqlDataSource dataSource,
        string actionType,
        string resourceId)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT
                audit_metadata->>'operation',
                audit_metadata->>'contractId',
                audit_metadata->>'auditEvidenceId'
            FROM access_audit_events
            WHERE actor_principal_id = @actor_principal_id
                AND action_type = @action_type
                AND resource_id = @resource_id
            ORDER BY occurred_at DESC, id DESC
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("actor_principal_id", ActorPrincipalId);
        command.Parameters.AddWithValue("action_type", actionType);
        command.Parameters.AddWithValue("resource_id", resourceId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Access audit metadata was not returned.");
        }

        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }
}
