using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Authentication;
using MemorySystem.Infrastructure.AccessAuditing;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminAuditExportTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid TargetPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OrgId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid OtherOrgId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid OtherProjectId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid OtherPrincipalId = Guid.Parse("77777777-7777-4777-8777-777777777777");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_audit_export_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_audit_export_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareAuditExportFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "/api/admin/audit-exports",
                CreateExportBody());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_audit_export_returns_ndjson_manifest_rows_hash_and_records_export_audit()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_audit_export_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareAuditExportFixtureAsync(databaseConnectionString);
            await InsertAccessAuditEvidenceAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/audit-exports",
                CreateExportBody()));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("application/x-ndjson", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
            Assert.Contains("attachment;", response.Content.Headers.ContentDisposition?.ToString() ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("rawPayload", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("memoryText", body, StringComparison.OrdinalIgnoreCase);

            var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(5, lines.Length);

            using var manifestDocument = JsonDocument.Parse(lines[0]);
            var manifest = manifestDocument.RootElement;
            Assert.Equal("manifest", manifest.GetProperty("recordType").GetString());
            Assert.Equal("memory.access-audit-export.v1", manifest.GetProperty("schemaVersion").GetString());
            Assert.Equal("ndjson", manifest.GetProperty("format").GetString());
            Assert.Equal(4, manifest.GetProperty("rowCount").GetInt32());
            Assert.Equal("project", manifest.GetProperty("filters").GetProperty("scopeType").GetString());
            Assert.Equal(ProjectId.ToString("D"), manifest.GetProperty("filters").GetProperty("scopeId").GetString());

            var rowContent = string.Join('\n', lines.Skip(1)) + "\n";
            var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rowContent))).ToLowerInvariant();
            Assert.Equal(expectedHash, manifest.GetProperty("contentSha256").GetString());

            var rowActionTypes = lines
                .Skip(1)
                .Select(line => JsonDocument.Parse(line))
                .Select(document => document.RootElement.GetProperty("actionType").GetString())
                .ToArray();
            Assert.Contains(AccessAuditActionTypes.Authentication, rowActionTypes);
            Assert.Contains(AccessAuditActionTypes.ProjectMembershipChange, rowActionTypes);
            Assert.Contains(AccessAuditActionTypes.NamespaceGrantChange, rowActionTypes);

            var exportId = manifest.GetProperty("exportId").GetGuid();
            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Equal(1L, await CountAuditExportRecordsAsync(dataSource, exportId, expectedHash));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_audit_export_rejects_invalid_time_window()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_audit_export_invalid_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareAuditExportFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/audit-exports",
                new
                {
                    occurredFrom = DateTimeOffset.UtcNow,
                    occurredTo = DateTimeOffset.UtcNow.AddMinutes(-1),
                    scopeType = "project",
                    scopeId = ProjectId,
                    limit = 100
                }));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("occurredFrom must be earlier than or equal to occurredTo.", body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task PrepareAuditExportFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ActorPrincipalId, displayName: "Access Admin");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, TargetPrincipalId, displayName: "Managed User");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, OtherPrincipalId, displayName: "Other User");
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgId, ProjectId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OtherOrgId, OtherProjectId);
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, ActorPrincipalId, "admin");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, TargetPrincipalId, "reader");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, OtherProjectId, OtherPrincipalId, "reader");
    }

    private static async Task InsertAccessAuditEvidenceAsync(string connectionString)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var store = new PostgresAccessAuditEventStore(dataSource);

        await store.RecordAsync(new AccessAuditEventCommand(
            AccessAuditActionTypes.Authentication,
            AccessAuditOutcomes.Succeeded,
            ActorPrincipalId: ActorPrincipalId,
            PrincipalType: "human",
            AuthMethod: AuthenticationMethods.ApiKey,
            CredentialId: TestApiKey,
            RequestMethod: "GET",
            RequestPath: "/api/memory/context"));

        await store.RecordAsync(new AccessAuditEventCommand(
            AccessAuditActionTypes.ProjectMembershipChange,
            AccessAuditOutcomes.Succeeded,
            ActorPrincipalId: ActorPrincipalId,
            TargetPrincipalId: TargetPrincipalId,
            ScopeType: "project",
            ScopeId: ProjectId.ToString("D"),
            ResourceType: "project_membership",
            ResourceId: $"{ProjectId:D}:{TargetPrincipalId:D}",
            Metadata: new Dictionary<string, string?> { ["accessLevel"] = "reader" }));

        await store.RecordAsync(new AccessAuditEventCommand(
            AccessAuditActionTypes.NamespaceGrantChange,
            AccessAuditOutcomes.Succeeded,
            ActorPrincipalId: ActorPrincipalId,
            TargetPrincipalId: TargetPrincipalId,
            NamespacePrefix: $"/project/{ProjectId}/decisions",
            Permission: MemoryAccessPermissions.Read,
            ResourceType: "memory_access_grant",
            ResourceId: Guid.NewGuid().ToString("D")));

        await store.RecordAsync(new AccessAuditEventCommand(
            AccessAuditActionTypes.ProjectMembershipChange,
            AccessAuditOutcomes.Succeeded,
            ActorPrincipalId: OtherPrincipalId,
            TargetPrincipalId: OtherPrincipalId,
            ScopeType: "project",
            ScopeId: OtherProjectId.ToString("D"),
            ResourceType: "project_membership",
            ResourceId: $"{OtherProjectId:D}:{OtherPrincipalId:D}"));
    }

    private static object CreateExportBody()
    {
        return new
        {
            occurredFrom = DateTimeOffset.UtcNow.AddMinutes(-10),
            occurredTo = DateTimeOffset.UtcNow.AddMinutes(10),
            scopeType = "project",
            scopeId = ProjectId,
            limit = 100
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

    private static async Task<long> CountAuditExportRecordsAsync(
        NpgsqlDataSource dataSource,
        Guid exportId,
        string contentSha256)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT count(*)
            FROM access_audit_events
            WHERE action_type = 'audit_export'
                AND resource_id = @export_id
                AND audit_metadata->>'contentSha256' = @content_sha256;
            """);
        command.Parameters.AddWithValue("export_id", exportId.ToString("D"));
        command.Parameters.AddWithValue("content_sha256", contentSha256);

        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Audit export count was not returned."));
    }
}
