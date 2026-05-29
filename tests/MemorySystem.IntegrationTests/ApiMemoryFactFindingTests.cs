using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiMemoryFactFindingTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgAId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectAId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgBId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectBId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [Fact]
    public async Task Post_query_facts_requires_authentication()
    {
        using var factory = CreateFactory("Host=127.0.0.1;Port=55432;Database=memory_system;Username=memory_system;Password=memory_system_dev_password");
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/memory/query-facts")
        {
            Content = JsonBody(new
            {
                query = "schema migrations"
            })
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_query_facts_returns_problem_details_for_invalid_request()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_query_facts_invalid_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/memory/query-facts")
            {
                Content = JsonBody(new
                {
                    query = "schema migrations",
                    targetScope = new
                    {
                        scopeType = "project"
                    }
                })
            };
            request.Headers.Add("X-Api-Key", TestApiKey);

            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseBody);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Memory fact query is invalid.", document.RootElement.GetProperty("title").GetString());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_query_facts_returns_authorized_facts_with_evidence_policy_and_safe_exclusions()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_query_facts_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareFactFindingFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendQueryFactsAsync(
                client,
                new
                {
                    query = "schema migrations",
                    targetScope = new
                    {
                        scopeType = "project",
                        scopeId = ProjectAId.ToString()
                    },
                    memoryTypes = new[] { "decision" },
                    includeContradictions = true,
                    includeExcluded = true,
                    limit = 1
                });

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Equal("schema migrations", payload.GetProperty("query").GetString());
            Assert.Equal("project", payload.GetProperty("targetScope").GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), payload.GetProperty("targetScope").GetProperty("scopeId").GetString());

            var fact = Assert.Single(payload.GetProperty("facts").EnumerateArray());
            Assert.Equal(fixture.ActiveFactId, fact.GetProperty("id").GetGuid());
            Assert.Equal("Schema migrations use SQL-first migrations plus raw Npgsql.", fact.GetProperty("claim").GetString());
            Assert.Equal("decision", fact.GetProperty("memoryType").GetString());
            Assert.Equal("active", fact.GetProperty("status").GetString());
            Assert.Equal(0.950m, fact.GetProperty("confidence").GetDecimal());
            Assert.Equal(ProjectAId.ToString(), fact.GetProperty("scopeId").GetString());
            Assert.Equal($"/api/events/{fixture.ActiveEventId}", Assert.Single(fact.GetProperty("sourceLinks").EnumerateArray()).GetString());

            var policy = fact.GetProperty("policy");
            Assert.True(policy.GetProperty("authorized").GetBoolean());
            Assert.Equal("user_scoped", policy.GetProperty("trustLevel").GetString());
            Assert.Equal("none", policy.GetProperty("sensitivity").GetString());
            Assert.Equal("active", policy.GetProperty("lifecycleStatus").GetString());
            Assert.True(policy.GetProperty("evidenceCurrent").GetBoolean());

            var contradiction = Assert.Single(payload.GetProperty("contradictions").EnumerateArray());
            Assert.Equal(fixture.ActiveFactId, contradiction.GetProperty("currentFactId").GetGuid());
            Assert.Equal(fixture.SupersededFactId, contradiction.GetProperty("relatedFactId").GetGuid());
            Assert.Equal("superseded", contradiction.GetProperty("relatedStatus").GetString());

            var exclusions = payload.GetProperty("excluded").EnumerateArray().ToArray();
            Assert.Contains(exclusions, exclusion =>
                exclusion.GetProperty("reason").GetString() == "inactive"
                && exclusion.GetProperty("count").GetInt32() == 1);
            Assert.Contains(exclusions, exclusion =>
                exclusion.GetProperty("reason").GetString() == "redacted_or_deleted"
                && exclusion.GetProperty("count").GetInt32() == 1);
            Assert.Contains(exclusions, exclusion =>
                exclusion.GetProperty("reason").GetString() == "not_authorized"
                && exclusion.GetProperty("count").ValueKind == JsonValueKind.Null
                && exclusion.GetProperty("countDisclosure").GetString() == "withheld");

            Assert.Equal(0.950m, payload.GetProperty("overallConfidence").GetDecimal());
            Assert.DoesNotContain("Project B confidential strategy", responseBody, StringComparison.Ordinal);
            Assert.DoesNotContain("redacted source should stay hidden", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_query_facts_hides_role_namespace_without_matching_assignment()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_query_facts_role_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);
            await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(databaseConnectionString, OrgAId, ProjectAId);
            await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
                databaseConnectionString,
                ProjectAId,
                PrincipalId,
                "reader");
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{ProjectAId}/role/cto/lens",
                "read",
                principalId: PrincipalId);

            var sourceEventId = Guid.NewGuid();
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                sourceEventId,
                PrincipalId,
                "project",
                ProjectAId.ToString(),
                scopeOrgId: OrgAId,
                scopeProjectId: ProjectAId);
            await InsertProjectMemoryFactAsync(
                databaseConnectionString,
                ProjectAId,
                OrgAId,
                sourceEventId,
                "role namespace sentinel",
                "uses",
                "secret CTO-only architecture direction",
                namespaceValue: $"/project/{ProjectAId}/role/cto/lens");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendQueryFactsAsync(
                client,
                new
                {
                    query = "role namespace sentinel",
                    targetScope = new
                    {
                        scopeType = "project",
                        scopeId = ProjectAId.ToString()
                    },
                    roleId = "cto",
                    includeExcluded = true
                });

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Empty(payload.GetProperty("facts").EnumerateArray());
            Assert.DoesNotContain("secret CTO-only architecture direction", responseBody, StringComparison.Ordinal);
            Assert.Contains(payload.GetProperty("excluded").EnumerateArray(), exclusion =>
                exclusion.GetProperty("reason").GetString() == "not_authorized"
                && exclusion.GetProperty("count").ValueKind == JsonValueKind.Null);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task<FactFindingFixture> PrepareFactFindingFixtureAsync(string connectionString)
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

        var activeEventId = Guid.NewGuid();
        var supersededEventId = Guid.NewGuid();
        var redactedEventId = Guid.NewGuid();
        var projectBEventId = Guid.NewGuid();

        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            activeEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            supersededEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            redactedEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            projectBEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId);

        var activeFactId = await InsertProjectMemoryFactAsync(
            connectionString,
            ProjectAId,
            OrgAId,
            activeEventId,
            "schema migrations",
            "use",
            "SQL-first migrations plus raw Npgsql");
        var supersededFactId = await InsertProjectMemoryFactAsync(
            connectionString,
            ProjectAId,
            OrgAId,
            supersededEventId,
            "schema migrations",
            "use",
            "ORM-first migrations",
            status: "superseded");
        await InsertProjectMemoryFactAsync(
            connectionString,
            ProjectAId,
            OrgAId,
            redactedEventId,
            "schema migrations",
            "use",
            "redacted source should stay hidden",
            status: "redacted");
        await InsertProjectMemoryFactAsync(
            connectionString,
            ProjectBId,
            OrgBId,
            projectBEventId,
            "schema migrations",
            "use",
            "Project B confidential strategy");

        return new FactFindingFixture(activeEventId, activeFactId, supersededFactId);
    }

    private static async Task<Guid> InsertProjectMemoryFactAsync(
        string connectionString,
        Guid projectId,
        Guid orgId,
        Guid sourceEventId,
        string subject,
        string predicate,
        string objectValue,
        string memoryType = "decision",
        string status = "active",
        string? namespaceValue = null)
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
                trust_level,
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
                @memory_type,
                'project_shared',
                @subject,
                @predicate,
                @object,
                0.950,
                'user_scoped',
                @status,
                @source_event_id,
                @principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("project_id_text", projectId.ToString());
        command.Parameters.AddWithValue("namespace", namespaceValue ?? $"/project/{projectId}/decisions");
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("memory_type", memoryType);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("predicate", predicate);
        command.Parameters.Add("object", NpgsqlDbType.Text).Value = objectValue;
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("principal_id", PrincipalId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendQueryFactsAsync(
        HttpClient client,
        object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/memory/query-facts")
        {
            Content = JsonBody(body)
        };
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static StringContent JsonBody(object value)
    {
        return new StringContent(
            JsonSerializer.Serialize(value),
            Encoding.UTF8,
            "application/json");
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, PrincipalId.ToString());
    }

    private sealed record FactFindingFixture(
        Guid ActiveEventId,
        Guid ActiveFactId,
        Guid SupersededFactId);
}
