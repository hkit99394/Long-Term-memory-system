using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminPermissionDriftReportTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid TargetPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid DisabledPrincipalId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid ServicePrincipalId = Guid.Parse("88888888-8888-4888-8888-888888888888");
    private static readonly Guid ServiceCredentialId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly Guid OrgId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_permission_drift_report_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_permission_drift_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PreparePermissionDriftFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "/api/admin/access/permission-drift",
                CreateReportBody());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_permission_drift_report_returns_payload_safe_findings_and_effective_previews()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_permission_drift_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PreparePermissionDriftFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/access/permission-drift",
                CreateReportBody()));
            var body = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(body);
            var root = payload.RootElement;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("gc02.target@example.test", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://idp.gc02.example.test", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sha256:gc02-service-key", body, StringComparison.OrdinalIgnoreCase);

            Assert.Equal("project", root.GetProperty("scope").GetProperty("scopeType").GetString());
            Assert.Equal(ProjectId, root.GetProperty("scope").GetProperty("scopeId").GetGuid());
            Assert.Equal($"/project/{ProjectId:D}", root.GetProperty("namespacePrefix").GetString());
            Assert.Equal(30, root.GetProperty("staleAfterDays").GetInt32());
            Assert.NotEqual(Guid.Empty, root.GetProperty("reportId").GetGuid());

            Assert.Contains(
                root.GetProperty("principals").EnumerateArray(),
                principal => principal.GetProperty("principalId").GetGuid() == DisabledPrincipalId
                    && principal.GetProperty("status").GetString() == "disabled");
            Assert.Contains(
                root.GetProperty("identityBindings").EnumerateArray(),
                binding => binding.GetProperty("principalId").GetGuid() == TargetPrincipalId
                    && binding.TryGetProperty("issuerHash", out var issuerHash)
                    && issuerHash.GetString()?.Length == 64);
            Assert.Contains(
                root.GetProperty("serviceCredentials").EnumerateArray(),
                credential => credential.GetProperty("credentialId").GetGuid() == ServiceCredentialId
                    && credential.GetProperty("status").GetString() == "active");
            Assert.Contains(
                root.GetProperty("namespaceGrants").EnumerateArray(),
                grant => grant.GetProperty("permission").GetString() == "admin"
                    && grant.GetProperty("namespacePrefix").GetString() == $"/project/{ProjectId:D}");
            Assert.Contains(
                root.GetProperty("effectiveAccessPreviews").EnumerateArray(),
                preview => preview.GetProperty("principalId").GetGuid() == TargetPrincipalId
                    && preview.GetProperty("permission").GetString() == "admin"
                    && preview.GetProperty("allowed").GetBoolean()
                    && preview.GetProperty("evaluatedBy").GetString() == "IMemoryAccessAuthorizer");

            var findingCodes = root.GetProperty("findings")
                .EnumerateArray()
                .Select(finding => finding.GetProperty("code").GetString())
                .ToArray();

            Assert.Contains("inactive_principal_has_access", findingCodes);
            Assert.Contains("stale_identity_binding", findingCodes);
            Assert.Contains("service_credential_expired", findingCodes);
            Assert.Contains("stale_service_credential", findingCodes);
            Assert.Contains("over_broad_namespace_admin_grant", findingCodes);
            Assert.Contains("broad_namespace_prefix", findingCodes);
            Assert.Contains("effective_admin_access", findingCodes);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task PreparePermissionDriftFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ActorPrincipalId, displayName: "Access Admin");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, TargetPrincipalId, displayName: "Managed User");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, DisabledPrincipalId, displayName: "Disabled User");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ServicePrincipalId, principalType: "service", displayName: "GC-02 Service");
        await DisablePrincipalAsync(connectionString, DisabledPrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgId, ProjectId);
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, ActorPrincipalId, "admin");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, TargetPrincipalId, "admin");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, DisabledPrincipalId, "reader");
        await ApiDatabaseTestSupport.InsertRoleAssignmentAsync(connectionString, TargetPrincipalId, "cto", "global");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectId:D}",
            "admin",
            principalId: TargetPrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectId:D}",
            "admin",
            principalId: ActorPrincipalId);
        await InsertIdentityBindingAsync(connectionString);
        await InsertServiceAccountAsync(connectionString);
    }

    private static async Task DisablePrincipalAsync(string connectionString, Guid principalId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE principals
            SET status = 'disabled'
            WHERE id = @principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", principalId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertIdentityBindingAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO identity_bindings (
                id,
                provider,
                issuer,
                subject,
                principal_id,
                status,
                external_display_name,
                external_email,
                external_tenant_id,
                provider_metadata,
                last_seen_at
            )
            VALUES (
                @binding_id,
                'oidc',
                'https://idp.gc02.example.test',
                'raw-subject-should-not-leak',
                @principal_id,
                'active',
                'GC02 Target',
                'gc02.target@example.test',
                'tenant-gc02',
                @provider_metadata,
                now() - interval '120 days'
            );
            """,
            connection);
        command.Parameters.AddWithValue("binding_id", Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
        command.Parameters.AddWithValue("principal_id", TargetPrincipalId);
        command.Parameters.Add("provider_metadata", NpgsqlDbType.Jsonb).Value = """{"source":"gc-02-test"}""";

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertServiceAccountAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO service_accounts (
                principal_id,
                owner_project_id,
                owner_principal_id,
                allowed_auth_method,
                review_due_at,
                expires_at,
                created_by_principal_id
            )
            VALUES (
                @service_principal_id,
                @project_id,
                @actor_principal_id,
                'api_key',
                now() - interval '10 days',
                now() + interval '60 days',
                @actor_principal_id
            );

            INSERT INTO service_account_credentials (
                id,
                service_principal_id,
                credential_label,
                auth_method,
                credential_fingerprint,
                status,
                review_due_at,
                expires_at,
                last_used_at,
                created_by_principal_id
            )
            VALUES (
                @service_credential_id,
                @service_principal_id,
                'gc-02-api-key',
                'api_key',
                'sha256:gc02-service-key',
                'active',
                now() - interval '5 days',
                now() - interval '1 day',
                NULL,
                @actor_principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("service_principal_id", ServicePrincipalId);
        command.Parameters.AddWithValue("service_credential_id", ServiceCredentialId);
        command.Parameters.AddWithValue("project_id", ProjectId);
        command.Parameters.AddWithValue("actor_principal_id", ActorPrincipalId);

        await command.ExecuteNonQueryAsync();
    }

    private static object CreateReportBody()
    {
        return new
        {
            scopeType = "project",
            scopeId = ProjectId,
            namespacePrefix = $"/project/{ProjectId:D}",
            staleAfterDays = 30,
            maxPreviewPrincipals = 10
        };
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
}
