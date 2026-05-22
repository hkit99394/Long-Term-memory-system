using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiMemoryFactReadTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgAId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectAId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgBId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectBId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid ProjectAEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid ProjectBEventId = Guid.Parse("77777777-7777-4777-8777-777777777777");

    [Fact]
    [Trait("Category", "Database")]
    public async Task Get_memory_fact_hides_cross_project_memory_without_explicit_access()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_cross_project_read_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var (projectAMemoryId, projectBMemoryId) = await PrepareCrossProjectFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (authorizedStatus, authorizedPayload, _) = await SendMemoryReadAsync(client, projectAMemoryId);

            Assert.Equal(HttpStatusCode.OK, authorizedStatus);
            Assert.Equal(projectAMemoryId, authorizedPayload.GetProperty("id").GetGuid());
            Assert.Equal("Project A decision", authorizedPayload.GetProperty("subject").GetString());

            var (blockedStatus, blockedPayload, blockedBody) = await SendMemoryReadAsync(client, projectBMemoryId);

            Assert.Equal(HttpStatusCode.NotFound, blockedStatus);
            Assert.Equal("Memory fact was not found.", blockedPayload.GetProperty("title").GetString());
            Assert.Contains("not accessible", blockedPayload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Project B decision", blockedBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendMemoryReadAsync(
        HttpClient client,
        Guid memoryFactId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/memory/{memoryFactId}");
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, PrincipalId.ToString());
    }

    private static async Task<(Guid ProjectAMemoryId, Guid ProjectBMemoryId)> PrepareCrossProjectFixtureAsync(
        string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgAId, ProjectAId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgBId, ProjectBId);
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
            connectionString,
            ProjectAId,
            PrincipalId,
            "reader");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/decisions",
            "read",
            principalId: PrincipalId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectAEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectBEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId);

        var projectAMemoryId = await InsertProjectMemoryFactAsync(
            connectionString,
            ProjectAId,
            OrgAId,
            ProjectAEventId,
            "Project A decision",
            "postgres");
        var projectBMemoryId = await InsertProjectMemoryFactAsync(
            connectionString,
            ProjectBId,
            OrgBId,
            ProjectBEventId,
            "Project B decision",
            "dynamodb");

        return (projectAMemoryId, projectBMemoryId);
    }

    private static async Task<Guid> InsertProjectMemoryFactAsync(
        string connectionString,
        Guid projectId,
        Guid orgId,
        Guid sourceEventId,
        string subject,
        string objectValue)
    {
        var memoryFactId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_facts (
                id,
                scope_type,
                scope_id,
                namespace,
                project_id,
                org_id,
                memory_type,
                visibility,
                subject,
                predicate,
                object,
                confidence,
                status,
                source_event_id,
                proposed_by_principal_id
            )
            VALUES (
                @memory_fact_id,
                'project',
                @project_id_text,
                @namespace,
                @project_id,
                @org_id,
                'decision',
                'project_shared',
                @subject,
                'uses',
                @object,
                0.950,
                'active',
                @source_event_id,
                @principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("project_id_text", projectId.ToString());
        command.Parameters.AddWithValue("namespace", $"/project/{projectId}/decisions");
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.Add("object", NpgsqlDbType.Text).Value = objectValue;
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("principal_id", PrincipalId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }
}
