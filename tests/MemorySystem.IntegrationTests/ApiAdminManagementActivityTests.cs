using System.Net;
using System.Text.Json;
using MemorySystem.Application.AccessAuditing;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminManagementActivityTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OtherPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid TargetPrincipalId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_management_activity_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm07_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync($"/api/admin/projects/{ProjectId:D}/management-activity");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_management_activity_returns_payload_safe_project_and_org_timelines()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm07_timeline_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var projectResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectId:D}/management-activity?limit=2"));
            using var projectPayload = await ReadJsonAsync(projectResponse);

            Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
            Assert.Equal("OPM-07", projectPayload.RootElement.GetProperty("contractId").GetString());
            Assert.True(projectPayload.RootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(projectPayload.RootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
            Assert.Equal("2", projectPayload.RootElement.GetProperty("nextCursor").GetString());
            Assert.Equal("project", projectPayload.RootElement.GetProperty("scope").GetProperty("scopeType").GetString());
            Assert.Equal(ProjectId, projectPayload.RootElement.GetProperty("scope").GetProperty("projectId").GetGuid());

            var projectEntries = projectPayload.RootElement.GetProperty("entries").EnumerateArray().ToArray();
            Assert.Equal(2, projectEntries.Length);
            Assert.Contains(projectEntries, entry => entry.GetProperty("actionType").GetString() == AccessAuditActionTypes.NamespaceGrantChange);
            Assert.Contains(projectEntries, entry => entry.GetProperty("actionType").GetString() == AccessAuditActionTypes.ProjectLifecycleChange);

            var grantEntry = projectEntries.First(entry => entry.GetProperty("actionType").GetString() == AccessAuditActionTypes.NamespaceGrantChange);
            Assert.Equal("succeeded", grantEntry.GetProperty("outcome").GetString());
            Assert.Equal("grant_matrix_replaced", grantEntry.GetProperty("operation").GetString());
            Assert.Equal("OPM-05", grantEntry.GetProperty("sourceContractId").GetString());
            Assert.Equal("opm07-grant-matrix-001", grantEntry.GetProperty("auditEvidenceId").GetString());
            Assert.Contains("Grant matrix replaced", grantEntry.GetProperty("summary").GetString(), StringComparison.Ordinal);

            var metadata = ReadMetadata(grantEntry);
            Assert.Equal("OPM-05", metadata["contractId"]);
            Assert.Equal("project_knowledge_steward", metadata["presetId"]);
            Assert.Equal("1", metadata["previousGrantCount"]);
            Assert.Equal("3", metadata["newGrantCount"]);
            Assert.False(metadata.ContainsKey("reason"));
            Assert.False(metadata.ContainsKey("rawPayload"));

            using var registrationPageResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectId:D}/management-activity?limit=2&cursor=2"));
            using var registrationPagePayload = await ReadJsonAsync(registrationPageResponse);
            var registrationEntry = Assert.Single(registrationPagePayload.RootElement.GetProperty("entries").EnumerateArray());
            Assert.Equal(AccessAuditActionTypes.ProjectRegistration, registrationEntry.GetProperty("actionType").GetString());

            using var orgResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/organizations/{OrgId:D}/management-activity?limit=10"));
            using var orgPayload = await ReadJsonAsync(orgResponse);
            Assert.Equal(HttpStatusCode.OK, orgResponse.StatusCode);
            Assert.Equal("org", orgPayload.RootElement.GetProperty("scope").GetProperty("scopeType").GetString());
            var orgActions = orgPayload.RootElement.GetProperty("entries")
                .EnumerateArray()
                .Select(entry => entry.GetProperty("actionType").GetString())
                .ToArray();
            Assert.Contains(AccessAuditActionTypes.ProjectRegistration, orgActions);
            Assert.Contains(AccessAuditActionTypes.ProjectLifecycleChange, orgActions);
            Assert.Contains(AccessAuditActionTypes.NamespaceGrantChange, orgActions);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_management_activity_rejects_forbidden_operator()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm07_forbidden_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString, OtherPrincipalId);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectId:D}/management-activity"));
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
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, TargetPrincipalId, displayName: "Knowledge Steward");
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            OrgId,
            ProjectId,
            organizationName: "OPM-07 Organization",
            projectName: "OPM-07 Project",
            projectStatus: "active");
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");

        await InsertAuditEventAsync(
            connectionString,
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            AccessAuditActionTypes.ProjectRegistration,
            "project_registration",
            ProjectId.ToString("D"),
            "POST",
            "/api/admin/projects/register",
            DateTimeOffset.Parse("2026-06-09T10:00:00Z"),
            """
            {
              "contractId": "REG-02",
              "operation": "registered",
              "auditEvidenceId": "opm07-registration-001",
              "idempotencyRecordId": "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
              "registrationRequestHash": "sha256:opm07",
              "accessPreviewReportId": "preview-opm07",
              "auditExportId": "audit-export-opm07",
              "sourceDocumentCount": "2",
              "sourceHashCoveragePercent": "100",
              "reason": "Registration note must stay hidden"
            }
            """);
        await InsertAuditEventAsync(
            connectionString,
            Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
            AccessAuditActionTypes.ProjectLifecycleChange,
            "project_lifecycle",
            ProjectId.ToString("D"),
            "PATCH",
            $"/api/admin/projects/{ProjectId:D}/lifecycle",
            DateTimeOffset.Parse("2026-06-09T11:00:00Z"),
            """
            {
              "contractId": "OPM-03",
              "operation": "updated",
              "auditEvidenceId": "opm07-lifecycle-001",
              "previousProjectStatus": "planned",
              "projectStatus": "active",
              "reason": "Lifecycle reason must stay hidden"
            }
            """);
        await InsertAuditEventAsync(
            connectionString,
            Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
            AccessAuditActionTypes.NamespaceGrantChange,
            "grant_matrix",
            $"{ProjectId:D}:knowledge_steward",
            "PUT",
            $"/api/admin/projects/{ProjectId:D}/grant-matrix/roles/knowledge_steward",
            DateTimeOffset.Parse("2026-06-09T12:00:00Z"),
            """
            {
              "contractId": "OPM-05",
              "operation": "grant_matrix_replaced",
              "auditEvidenceId": "opm07-grant-matrix-001",
              "presetId": "project_knowledge_steward",
              "previousGrantCount": "1",
              "newGrantCount": "3",
              "reason": "Grant reason must stay hidden"
            }
            """,
            roleId: "knowledge_steward",
            namespacePrefix: $"/project/{ProjectId:D}/facts",
            permission: "read",
            targetPrincipalId: TargetPrincipalId);
    }

    private static async Task InsertAuditEventAsync(
        string connectionString,
        Guid auditEventId,
        string actionType,
        string resourceType,
        string resourceId,
        string requestMethod,
        string requestPath,
        DateTimeOffset occurredAt,
        string metadataJson,
        string? roleId = null,
        string? namespacePrefix = null,
        string? permission = null,
        Guid? targetPrincipalId = null)
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
                target_principal_id,
                scope_type,
                scope_id,
                role_id,
                namespace_prefix,
                permission,
                resource_type,
                resource_id,
                request_method,
                request_path,
                correlation_id,
                audit_metadata,
                occurred_at
            )
            VALUES (
                @id,
                @action_type,
                'succeeded',
                @actor_principal_id,
                @target_principal_id,
                'project',
                @project_id_text,
                @role_id,
                @namespace_prefix,
                @permission,
                @resource_type,
                @resource_id,
                @request_method,
                @request_path,
                @correlation_id,
                @audit_metadata,
                @occurred_at
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", auditEventId);
        command.Parameters.AddWithValue("action_type", actionType);
        command.Parameters.AddWithValue("actor_principal_id", ActorPrincipalId);
        command.Parameters.Add("target_principal_id", NpgsqlDbType.Uuid).Value =
            targetPrincipalId.HasValue ? targetPrincipalId.Value : DBNull.Value;
        command.Parameters.AddWithValue("project_id_text", ProjectId.ToString("D"));
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(roleId) ? DBNull.Value : roleId;
        command.Parameters.Add("namespace_prefix", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(namespacePrefix) ? DBNull.Value : namespacePrefix;
        command.Parameters.Add("permission", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(permission) ? DBNull.Value : permission;
        command.Parameters.AddWithValue("resource_type", resourceType);
        command.Parameters.AddWithValue("resource_id", resourceId);
        command.Parameters.AddWithValue("request_method", requestMethod);
        command.Parameters.AddWithValue("request_path", requestPath);
        command.Parameters.AddWithValue("correlation_id", $"opm07-{auditEventId:N}");
        command.Parameters.Add("audit_metadata", NpgsqlDbType.Jsonb).Value = metadataJson;
        command.Parameters.AddWithValue("occurred_at", occurredAt);

        await command.ExecuteNonQueryAsync();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string postgresConnectionString,
        Guid? targetActorPrincipalId = null)
    {
        return MemorySystemApiTestFactory.Create(
            postgresConnectionString,
            TestApiKey,
            (targetActorPrincipalId ?? ActorPrincipalId).ToString("D"));
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

    private static IReadOnlyDictionary<string, string> ReadMetadata(JsonElement entry)
    {
        return entry.GetProperty("metadata")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("key").GetString() ?? string.Empty,
                item => item.GetProperty("value").GetString() ?? string.Empty,
                StringComparer.Ordinal);
    }
}
