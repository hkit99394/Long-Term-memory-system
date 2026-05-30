using System.Net;
using System.Text.Json;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryFacts;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminMemoryConsoleTests
{
    private const string TestApiKey = "test-api-key";
    private const string AuthorizedSourcePayload = "authorized source evidence raw payload";
    private const string RedactedMemoryPayload = "redacted admin console secret";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgAId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectAId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgBId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectBId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_console_serves_static_assets()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_console_static_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var html = await client.GetStringAsync("/admin/");
            var script = await client.GetStringAsync("/admin/admin-console.js");

            Assert.Contains("Memory Admin", html, StringComparison.Ordinal);
            Assert.Contains("/admin/admin-console.js", html, StringComparison.Ordinal);
            Assert.Contains("/api/admin/memory/facts", script, StringComparison.Ordinal);
            Assert.Contains("sourceLink", script, StringComparison.Ordinal);
            Assert.Contains("sourceRetentionClass", script, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_memory_facts_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_memory_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/api/admin/memory/facts");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_memory_facts_lists_authorized_safe_metadata_and_opens_source_evidence()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_memory_list_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareAdminMemoryFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/admin/memory/facts?limit=20");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(body);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(fixture.UnauthorizedFactId.ToString(), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Project B admin console fact", body, StringComparison.Ordinal);
            Assert.DoesNotContain(AuthorizedSourcePayload, body, StringComparison.Ordinal);
            Assert.DoesNotContain(RedactedMemoryPayload, body, StringComparison.Ordinal);

            var facts = payload.RootElement.GetProperty("facts")
                .EnumerateArray()
                .ToDictionary(fact => fact.GetProperty("id").GetGuid());

            Assert.True(facts.ContainsKey(fixture.ActiveFactId));
            Assert.True(facts.ContainsKey(fixture.RedactedFactId));

            var active = facts[fixture.ActiveFactId];
            Assert.Equal("project", active.GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), active.GetProperty("scopeId").GetString());
            Assert.Equal($"/project/{ProjectAId}/decisions", active.GetProperty("namespace").GetString());
            Assert.Equal("decision", active.GetProperty("memoryType").GetString());
            Assert.Equal("Admin console source boundary", active.GetProperty("subject").GetString());
            Assert.Equal("records", active.GetProperty("predicate").GetString());
            Assert.Equal("safe source inspection", active.GetProperty("object").GetString());
            Assert.Equal(fixture.ActiveSourceEventId, active.GetProperty("sourceEventId").GetGuid());
            Assert.Equal($"/api/events/{fixture.ActiveSourceEventId}", active.GetProperty("sourceLink").GetString());

            var activePolicy = active.GetProperty("policy");
            Assert.True(activePolicy.GetProperty("contentVisible").GetBoolean());
            Assert.Equal("standard", activePolicy.GetProperty("sourceRetentionClass").GetString());
            Assert.Equal("personal", activePolicy.GetProperty("sourceSensitivity").GetString());
            Assert.Equal("human_approved", activePolicy.GetProperty("sourceTrustLevel").GetString());
            Assert.Equal("none", activePolicy.GetProperty("sourceRedactionStatus").GetString());
            Assert.False(activePolicy.GetProperty("sourcePayloadIncluded").GetBoolean());

            var redacted = facts[fixture.RedactedFactId];
            Assert.Equal("redacted", redacted.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, redacted.GetProperty("subject").ValueKind);
            Assert.Equal(JsonValueKind.Null, redacted.GetProperty("predicate").ValueKind);
            Assert.Equal(JsonValueKind.Null, redacted.GetProperty("object").ValueKind);
            var redactedPolicy = redacted.GetProperty("policy");
            Assert.False(redactedPolicy.GetProperty("contentVisible").GetBoolean());
            Assert.Equal("memory_content_hidden_by_lifecycle", redactedPolicy.GetProperty("contentVisibilityReason").GetString());

            using var sourceRequest = CreateAuthenticatedRequest(HttpMethod.Get, active.GetProperty("sourceLink").GetString()!);
            using var sourceResponse = await client.SendAsync(sourceRequest);
            var sourceBody = await sourceResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, sourceResponse.StatusCode);
            Assert.Contains(AuthorizedSourcePayload, sourceBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_memory_facts_filters_by_lifecycle_status()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_memory_status_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareAdminMemoryFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/admin/memory/facts?status=redacted&limit=20");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(body);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var fact = Assert.Single(payload.RootElement.GetProperty("facts").EnumerateArray());
            Assert.Equal(fixture.RedactedFactId, fact.GetProperty("id").GetGuid());
            Assert.Equal("redacted", fact.GetProperty("status").GetString());
            Assert.DoesNotContain(RedactedMemoryPayload, body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_memory_facts_query_does_not_match_hidden_content()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_memory_query_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareAdminMemoryFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            using var request = CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/api/admin/memory/facts?q={Uri.EscapeDataString(RedactedMemoryPayload)}&limit=20");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(body);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(payload.RootElement.GetProperty("facts").EnumerateArray());
            Assert.DoesNotContain(RedactedMemoryPayload, body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, PrincipalId.ToString());
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Api-Key", TestApiKey);

        return request;
    }

    private static async Task<AdminMemoryFixture> PrepareAdminMemoryFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgAId, ProjectAId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            OrgBId,
            ProjectBId,
            organizationName: "Other Org",
            projectName: "Other Project");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectAId, PrincipalId, "reader");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/decisions",
            "read",
            principalId: PrincipalId);

        var activeSourceEventId = Guid.NewGuid();
        var redactedSourceEventId = Guid.NewGuid();
        var unauthorizedSourceEventId = Guid.NewGuid();
        await InsertProjectSourceEventAsync(connectionString, activeSourceEventId, ProjectAId, OrgAId, "personal");
        await InsertProjectSourceEventAsync(connectionString, redactedSourceEventId, ProjectAId, OrgAId, "none");
        await InsertProjectSourceEventAsync(connectionString, unauthorizedSourceEventId, ProjectBId, OrgBId, "none");
        await UpdateEventContentAsync(connectionString, activeSourceEventId, AuthorizedSourcePayload);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new PostgresMemoryFactRepository(dataSource);
        var projectAScope = new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId);
        var projectBScope = new MemoryScopeResolution("project", ProjectBId.ToString(), OrgId: OrgBId, ProjectId: ProjectBId);

        var activeFact = await repository.StoreAsync(new MemoryFactWriteCommand(
            projectAScope,
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "Admin console source boundary",
            "records",
            "safe source inspection",
            0.910m,
            activeSourceEventId,
            PrincipalId,
            MemoryFactStatuses.Active));
        var redactedFact = await repository.StoreAsync(new MemoryFactWriteCommand(
            projectAScope,
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            RedactedMemoryPayload,
            "must hide",
            RedactedMemoryPayload,
            0.640m,
            redactedSourceEventId,
            PrincipalId,
            MemoryFactStatuses.Redacted));
        var unauthorizedFact = await repository.StoreAsync(new MemoryFactWriteCommand(
            projectBScope,
            $"/project/{ProjectBId}/decisions",
            "decision",
            "project_shared",
            "Project B admin console fact",
            "must not leak",
            "private project B memory",
            0.830m,
            unauthorizedSourceEventId,
            PrincipalId,
            MemoryFactStatuses.Active));

        return new AdminMemoryFixture(
            activeFact.Id,
            redactedFact.Id,
            unauthorizedFact.Id,
            activeSourceEventId);
    }

    private static async Task InsertProjectSourceEventAsync(
        string connectionString,
        Guid eventId,
        Guid projectId,
        Guid orgId,
        string sensitivity)
    {
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            eventId,
            PrincipalId,
            "project",
            projectId.ToString(),
            scopeOrgId: orgId,
            scopeProjectId: projectId,
            trustLevel: "human_approved",
            sensitivity: sensitivity);
    }

    private static async Task UpdateEventContentAsync(
        string connectionString,
        Guid eventId,
        string message)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE events
            SET content = @content
            WHERE id = @event_id;
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.Add("content", NpgsqlDbType.Jsonb).Value =
            $$"""{"message":"{{message}}"}""";

        await command.ExecuteNonQueryAsync();
    }

    private sealed record AdminMemoryFixture(
        Guid ActiveFactId,
        Guid RedactedFactId,
        Guid UnauthorizedFactId,
        Guid ActiveSourceEventId);
}
