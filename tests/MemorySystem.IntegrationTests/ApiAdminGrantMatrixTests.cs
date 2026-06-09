using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MemorySystem.Application.AccessAuditing;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminGrantMatrixTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid TargetPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OrgId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid RoleAssignmentId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid NamespaceGrantId = Guid.Parse("66666666-6666-4666-8666-666666666666");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_grant_matrix_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm05_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync($"/api/admin/projects/{ProjectId:D}/grant-matrix");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Project_grant_matrix_reads_presets_previews_updates_and_audits()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm05_matrix_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var matrixResponse = await client.SendAsync(CreateAuthenticatedGetRequest($"/api/admin/projects/{ProjectId:D}/grant-matrix"));
            using var matrixPayload = await ReadJsonAsync(matrixResponse);

            Assert.Equal(HttpStatusCode.OK, matrixResponse.StatusCode);
            Assert.Equal("OPM-05", matrixPayload.RootElement.GetProperty("contractId").GetString());
            Assert.True(matrixPayload.RootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(matrixPayload.RootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
            Assert.Equal("project_knowledge_steward", FindPreset(matrixPayload, "project_knowledge_steward").GetProperty("presetId").GetString());

            var initialRole = FindRole(matrixPayload, "knowledge_steward");
            Assert.Equal("knowledge_steward", initialRole.GetProperty("roleId").GetString());
            Assert.Equal("project_knowledge_steward", initialRole.GetProperty("recommendedPresetId").GetString());
            Assert.Equal("missing_preset_grants", initialRole.GetProperty("presetAlignment").GetString());
            Assert.Single(initialRole.GetProperty("grants").EnumerateArray());
            var preview = Assert.Single(initialRole.GetProperty("effectiveAccessPreviews").EnumerateArray());
            Assert.True(preview.GetProperty("allowed").GetBoolean());
            Assert.Equal("IMemoryAccessAuthorizer", preview.GetProperty("evaluatedBy").GetString());

            using var updateResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                $"/api/admin/projects/{ProjectId:D}/grant-matrix/roles/knowledge_steward",
                new
                {
                    roleId = "knowledge_steward",
                    presetId = "project_knowledge_steward",
                    grants = KnowledgeStewardPresetGrants().Select(grant => new
                    {
                        namespacePrefix = grant.NamespacePrefix,
                        permission = grant.Permission
                    }),
                    reason = "OPM-05 applies the knowledge steward least-privilege preset.",
                    auditEvidenceId = "opm05-knowledge-steward-001"
                }));
            using var updatePayload = await ReadJsonAsync(updateResponse);

            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            Assert.Equal("OPM-05", updatePayload.RootElement.GetProperty("contractId").GetString());
            Assert.Equal("updated", updatePayload.RootElement.GetProperty("status").GetString());
            Assert.Equal("matches_preset", updatePayload.RootElement.GetProperty("role").GetProperty("presetAlignment").GetString());
            Assert.Equal("namespace_grant_change", updatePayload.RootElement.GetProperty("auditEvidence").GetProperty("actionType").GetString());
            Assert.Equal("grant_matrix", updatePayload.RootElement.GetProperty("auditEvidence").GetProperty("resourceType").GetString());
            Assert.True(updatePayload.RootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(updatePayload.RootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Equal(KnowledgeStewardPresetGrants().Count, await CountRoleProjectGrantsAsync(dataSource));
            Assert.False(await HasAdminPermissionGrantAsync(dataSource));
            Assert.Equal(
                ("grant_matrix_replaced", "OPM-05", "project_knowledge_steward", "opm05-knowledge-steward-001"),
                await ReadAuditMetadataAsync(dataSource));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Project_grant_matrix_rejects_admin_and_project_root_grants()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm05_guard_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var adminResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                $"/api/admin/projects/{ProjectId:D}/grant-matrix/roles/knowledge_steward",
                new
                {
                    roleId = "knowledge_steward",
                    presetId = "custom",
                    grants = new[]
                    {
                        new
                        {
                            namespacePrefix = $"/project/{ProjectId:D}/facts",
                            permission = "admin"
                        }
                    },
                    reason = "Invalid admin grant.",
                    auditEvidenceId = "opm05-invalid-admin"
                }));
            var adminBody = await adminResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, adminResponse.StatusCode);
            Assert.Contains("Permission is not supported", adminBody, StringComparison.OrdinalIgnoreCase);

            using var rootResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                $"/api/admin/projects/{ProjectId:D}/grant-matrix/roles/knowledge_steward",
                new
                {
                    roleId = "knowledge_steward",
                    presetId = "custom",
                    grants = new[]
                    {
                        new
                        {
                            namespacePrefix = $"/project/{ProjectId:D}",
                            permission = "read"
                        }
                    },
                    reason = "Invalid root grant.",
                    auditEvidenceId = "opm05-invalid-root"
                }));
            var rootBody = await rootResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, rootResponse.StatusCode);
            Assert.Contains("Project root namespace grants must stay absent", rootBody, StringComparison.OrdinalIgnoreCase);
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
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, TargetPrincipalId, displayName: "Knowledge Steward");
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            OrgId,
            ProjectId,
            organizationName: "OPM-05 Organization",
            projectName: "OPM-05 Project",
            projectStatus: "active");
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, TargetPrincipalId, "contributor");
        await InsertRoleAssignmentAsync(connectionString);
        await InsertNamespaceGrantAsync(connectionString);
    }

    private static async Task InsertRoleAssignmentAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO role_assignments (id, principal_id, role_id, scope_type, scope_id)
            VALUES (@assignment_id, @principal_id, 'knowledge_steward', 'project', @project_id);
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
                'knowledge_steward',
                @namespace_prefix,
                'read'
            );
            """,
            connection);
        command.Parameters.AddWithValue("grant_id", NamespaceGrantId);
        command.Parameters.AddWithValue("namespace_prefix", $"/project/{ProjectId:D}/facts");

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
        if (path.Contains("/grant-matrix/", StringComparison.Ordinal))
        {
            request.Method = HttpMethod.Put;
        }

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

    private static JsonElement FindPreset(JsonDocument payload, string presetId)
    {
        return payload.RootElement.GetProperty("presets")
            .EnumerateArray()
            .First(preset => preset.GetProperty("presetId").GetString() == presetId);
    }

    private static IReadOnlyList<(string NamespacePrefix, string Permission)> KnowledgeStewardPresetGrants()
    {
        var projectRoot = $"/project/{ProjectId:D}";
        return
        [
            ($"{projectRoot}/facts", "read"),
            ($"{projectRoot}/facts", "write"),
            ($"{projectRoot}/facts", "review"),
            ($"{projectRoot}/decisions", "read"),
            ($"{projectRoot}/decisions", "write"),
            ($"{projectRoot}/decisions", "review"),
            ($"{projectRoot}/reviews", "review"),
            ($"{projectRoot}/role/knowledge_steward/lens", "read"),
            ($"{projectRoot}/role/knowledge_steward/lens", "write")
        ];
    }

    private static async Task<long> CountRoleProjectGrantsAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT count(*)
            FROM memory_access_grants
            WHERE principal_id IS NULL
                AND role_id = 'knowledge_steward'
                AND namespace_prefix LIKE @project_root_pattern;
            """);
        command.Parameters.AddWithValue("project_root_pattern", $"/project/{ProjectId:D}/%");
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<bool> HasAdminPermissionGrantAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM memory_access_grants
                WHERE role_id = 'knowledge_steward'
                    AND permission = 'admin'
            );
            """);
        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<(string? Operation, string? ContractId, string? PresetId, string? AuditEvidenceId)> ReadAuditMetadataAsync(
        NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT
                audit_metadata->>'operation',
                audit_metadata->>'contractId',
                audit_metadata->>'presetId',
                audit_metadata->>'auditEvidenceId'
            FROM access_audit_events
            WHERE actor_principal_id = @actor_principal_id
                AND action_type = @action_type
                AND resource_type = 'grant_matrix'
                AND resource_id = @resource_id
            ORDER BY occurred_at DESC, id DESC
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("actor_principal_id", ActorPrincipalId);
        command.Parameters.AddWithValue("action_type", AccessAuditActionTypes.NamespaceGrantChange);
        command.Parameters.AddWithValue("resource_id", $"{ProjectId:D}:knowledge_steward");

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Grant matrix audit metadata was not returned.");
        }

        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }
}
