using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiVaultExportTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgAId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectAId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgBId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectBId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_obsidian_export_returns_authorized_approved_decisions_and_summaries_with_source_ids()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_obsidian_export_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareExportFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendObsidianExportAsync(client, limit: 10);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var documents = payload.GetProperty("documents").EnumerateArray().ToArray();
            var staleDocuments = payload.GetProperty("staleDocuments").EnumerateArray().ToArray();
            Assert.Equal(2, documents.Length);
            Assert.Empty(staleDocuments);

            Assert.Equal("decision", documents[0].GetProperty("memoryType").GetString());
            Assert.StartsWith($"20 Projects/{ProjectAId}/Decisions/", documents[0].GetProperty("path").GetString(), StringComparison.Ordinal);
            Assert.Equal(fixture.DecisionMemoryId, documents[0].GetProperty("memoryFactId").GetGuid());
            Assert.Equal(fixture.DecisionSourceEventId, documents[0].GetProperty("sourceEventId").GetGuid());
            Assert.Contains($"memory_fact_id: \"{fixture.DecisionMemoryId}\"", documents[0].GetProperty("content").GetString(), StringComparison.Ordinal);
            Assert.Contains($"source_event_id: \"{fixture.DecisionSourceEventId}\"", documents[0].GetProperty("content").GetString(), StringComparison.Ordinal);
            Assert.Contains($"/api/events/{fixture.DecisionSourceEventId}", documents[0].GetProperty("content").GetString(), StringComparison.Ordinal);

            Assert.Equal("summary", documents[1].GetProperty("memoryType").GetString());
            Assert.StartsWith($"20 Projects/{ProjectAId}/Summaries/", documents[1].GetProperty("path").GetString(), StringComparison.Ordinal);
            Assert.Equal(fixture.SummaryMemoryId, documents[1].GetProperty("memoryFactId").GetGuid());
            Assert.Equal(fixture.SummarySourceEventId, documents[1].GetProperty("sourceEventId").GetGuid());
            Assert.Contains($"source_event_id: \"{fixture.SummarySourceEventId}\"", documents[1].GetProperty("content").GetString(), StringComparison.Ordinal);

            Assert.DoesNotContain(fixture.ProjectBMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(fixture.ProjectAFactMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Project B private decision", responseBody, StringComparison.Ordinal);
            Assert.DoesNotContain("Project A operational fact", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_obsidian_export_marks_deleted_or_redacted_previous_exports_stale()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_obsidian_stale_export_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareExportFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (_, firstPayload, _) = await SendObsidianExportAsync(client, limit: 10);
            var firstDocuments = firstPayload.GetProperty("documents").EnumerateArray().ToArray();
            var decisionPath = firstDocuments.Single(document => document.GetProperty("memoryFactId").GetGuid() == fixture.DecisionMemoryId).GetProperty("path").GetString();
            var summaryPath = firstDocuments.Single(document => document.GetProperty("memoryFactId").GetGuid() == fixture.SummaryMemoryId).GetProperty("path").GetString();

            await UpdateMemoryFactStatusAsync(databaseConnectionString, fixture.DecisionMemoryId, "deleted");
            await UpdateMemoryFactStatusAsync(databaseConnectionString, fixture.SummaryMemoryId, "redacted");

            var (statusCode, secondPayload, responseBody) = await SendObsidianExportAsync(client, limit: 10);

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Empty(secondPayload.GetProperty("documents").EnumerateArray());

            var staleDocuments = secondPayload.GetProperty("staleDocuments").EnumerateArray().ToArray();
            Assert.Equal(2, staleDocuments.Length);

            var staleDecision = staleDocuments.Single(document => document.GetProperty("memoryFactId").GetGuid() == fixture.DecisionMemoryId);
            Assert.Equal(decisionPath, staleDecision.GetProperty("path").GetString());
            Assert.Equal("deleted", staleDecision.GetProperty("status").GetString());
            Assert.Equal("memory_deleted", staleDecision.GetProperty("reason").GetString());
            Assert.Contains("stale: true", staleDecision.GetProperty("content").GetString(), StringComparison.Ordinal);
            Assert.Contains($"source_event_id: \"{fixture.DecisionSourceEventId}\"", staleDecision.GetProperty("content").GetString(), StringComparison.Ordinal);

            var staleSummary = staleDocuments.Single(document => document.GetProperty("memoryFactId").GetGuid() == fixture.SummaryMemoryId);
            Assert.Equal(summaryPath, staleSummary.GetProperty("path").GetString());
            Assert.Equal("redacted", staleSummary.GetProperty("status").GetString());
            Assert.Equal("memory_redacted", staleSummary.GetProperty("reason").GetString());
            Assert.Contains("stale: true", staleSummary.GetProperty("content").GetString(), StringComparison.Ordinal);
            Assert.Contains($"source_event_id: \"{fixture.SummarySourceEventId}\"", staleSummary.GetProperty("content").GetString(), StringComparison.Ordinal);

            Assert.DoesNotContain("approved decisions as Obsidian Markdown with source IDs", responseBody, StringComparison.Ordinal);
            Assert.DoesNotContain("human-approved review outcomes for the project", responseBody, StringComparison.Ordinal);

            Assert.Equal("stale", await ReadVaultExportStatusAsync(databaseConnectionString, fixture.DecisionMemoryId, "obsidian_markdown"));
            Assert.Equal("stale", await ReadVaultExportStatusAsync(databaseConnectionString, fixture.SummaryMemoryId, "obsidian_markdown"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_obsidian_archive_export_returns_authorized_old_memory_in_readable_archive_format()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_obsidian_archive_export_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareExportFixtureAsync(databaseConnectionString);
            await UpdateMemoryFactStatusAsync(databaseConnectionString, fixture.DecisionMemoryId, "superseded");
            await UpdateMemoryFactStatusAsync(databaseConnectionString, fixture.ProjectAFactMemoryId, "expired");
            await UpdateMemoryFactStatusAsync(databaseConnectionString, fixture.SummaryMemoryId, "redacted");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendObsidianArchiveExportAsync(client, limit: 10);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var documents = payload.GetProperty("documents").EnumerateArray().ToArray();
            Assert.Equal(2, documents.Length);

            var archivedDecision = documents.Single(document => document.GetProperty("memoryFactId").GetGuid() == fixture.DecisionMemoryId);
            Assert.Equal("superseded", archivedDecision.GetProperty("status").GetString());
            Assert.StartsWith($"90 Archive/20 Projects/{ProjectAId}/superseded/Decisions/", archivedDecision.GetProperty("path").GetString(), StringComparison.Ordinal);
            Assert.Contains("archive: true", archivedDecision.GetProperty("content").GetString(), StringComparison.Ordinal);
            Assert.Contains("approved decisions as Obsidian Markdown with source IDs", archivedDecision.GetProperty("content").GetString(), StringComparison.Ordinal);
            Assert.Contains($"source_event_id: \"{fixture.DecisionSourceEventId}\"", archivedDecision.GetProperty("content").GetString(), StringComparison.Ordinal);

            var archivedFact = documents.Single(document => document.GetProperty("memoryFactId").GetGuid() == fixture.ProjectAFactMemoryId);
            Assert.Equal("expired", archivedFact.GetProperty("status").GetString());
            Assert.StartsWith($"90 Archive/20 Projects/{ProjectAId}/expired/Memory/", archivedFact.GetProperty("path").GetString(), StringComparison.Ordinal);
            Assert.Contains("facts are not part of the first Obsidian export", archivedFact.GetProperty("content").GetString(), StringComparison.Ordinal);

            Assert.DoesNotContain(fixture.SummaryMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("human-approved review outcomes for the project", responseBody, StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.ProjectBMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);

            Assert.Equal("current", await ReadVaultExportStatusAsync(databaseConnectionString, fixture.DecisionMemoryId, "obsidian_archive"));
            Assert.Equal("current", await ReadVaultExportStatusAsync(databaseConnectionString, fixture.ProjectAFactMemoryId, "obsidian_archive"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_obsidian_export_rejects_invalid_limit()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_obsidian_export_limit_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendObsidianExportAsync(client, limit: 101);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Obsidian export request is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("between 1 and 100", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task<ObsidianExportFixture> PrepareExportFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgAId, ProjectAId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgBId, ProjectBId);
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectAId, PrincipalId, "reader");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}",
            "read",
            principalId: PrincipalId);

        var decisionSourceEventId = Guid.NewGuid();
        var summarySourceEventId = Guid.NewGuid();
        var projectBSourceEventId = Guid.NewGuid();
        var projectAFactSourceEventId = Guid.NewGuid();

        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            decisionSourceEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "user_scoped");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            summarySourceEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "human_approved");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            projectBSourceEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId,
            trustLevel: "human_approved");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            projectAFactSourceEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "human_approved");

        var decisionMemoryId = await InsertProjectMemoryFactAsync(
            connectionString,
            ProjectAId,
            OrgAId,
            decisionSourceEventId,
            "decision",
            $"/project/{ProjectAId}/decisions",
            "Export source-linked decisions",
            "records",
            "approved decisions as Obsidian Markdown with source IDs",
            "user_scoped");
        await InsertApprovedReviewAsync(connectionString, decisionMemoryId, decisionSourceEventId);

        var summaryMemoryId = await InsertProjectMemoryFactAsync(
            connectionString,
            ProjectAId,
            OrgAId,
            summarySourceEventId,
            "summary",
            $"/project/{ProjectAId}/summaries",
            "Review workflow summary",
            "summarizes",
            "human-approved review outcomes for the project",
            "human_approved");

        var projectBMemoryId = await InsertProjectMemoryFactAsync(
            connectionString,
            ProjectBId,
            OrgBId,
            projectBSourceEventId,
            "decision",
            $"/project/{ProjectBId}/decisions",
            "Project B private decision",
            "uses",
            "a storage option Project A cannot read",
            "human_approved");

        var projectAFactMemoryId = await InsertProjectMemoryFactAsync(
            connectionString,
            ProjectAId,
            OrgAId,
            projectAFactSourceEventId,
            "fact",
            $"/project/{ProjectAId}/facts",
            "Project A operational fact",
            "records",
            "facts are not part of the first Obsidian export",
            "human_approved");

        return new ObsidianExportFixture(
            decisionMemoryId,
            decisionSourceEventId,
            summaryMemoryId,
            summarySourceEventId,
            projectBMemoryId,
            projectAFactMemoryId);
    }

    private static async Task<Guid> InsertProjectMemoryFactAsync(
        string connectionString,
        Guid projectId,
        Guid orgId,
        Guid sourceEventId,
        string memoryType,
        string namespaceValue,
        string subject,
        string predicate,
        string objectValue,
        string trustLevel)
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
                @trust_level,
                'active',
                @source_event_id,
                @principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("project_id_text", projectId.ToString());
        command.Parameters.AddWithValue("namespace", namespaceValue);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("memory_type", memoryType);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("predicate", predicate);
        command.Parameters.Add("object", NpgsqlDbType.Text).Value = objectValue;
        command.Parameters.AddWithValue("trust_level", trustLevel);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("principal_id", PrincipalId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }

    private static async Task InsertApprovedReviewAsync(
        string connectionString,
        Guid memoryFactId,
        Guid sourceEventId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_reviews (
                id,
                memory_fact_id,
                review_status,
                reviewer_id,
                notes,
                source_event_id
            )
            VALUES (
                @review_id,
                @memory_fact_id,
                'approved',
                @reviewer_id,
                'Approved for vault export.',
                @source_event_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("review_id", Guid.NewGuid());
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("reviewer_id", PrincipalId);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpdateMemoryFactStatusAsync(
        string connectionString,
        Guid memoryFactId,
        string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE memory_facts
            SET status = @status
            WHERE id = @memory_fact_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ReadVaultExportStatusAsync(
        string connectionString,
        Guid memoryFactId,
        string exportType)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT status
            FROM vault_exports
            WHERE export_type = @export_type
                AND memory_fact_id = @memory_fact_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("export_type", exportType);

        return await command.ExecuteScalarAsync() as string
            ?? throw new InvalidOperationException($"Vault export for memory fact {memoryFactId} was not found.");
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendObsidianExportAsync(
        HttpClient client,
        int limit)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/vault/exports/obsidian?limit={limit}");
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendObsidianArchiveExportAsync(
        HttpClient client,
        int limit)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/vault/exports/obsidian/archive?limit={limit}");
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

    private sealed record ObsidianExportFixture(
        Guid DecisionMemoryId,
        Guid DecisionSourceEventId,
        Guid SummaryMemoryId,
        Guid SummarySourceEventId,
        Guid ProjectBMemoryId,
        Guid ProjectAFactMemoryId);
}
