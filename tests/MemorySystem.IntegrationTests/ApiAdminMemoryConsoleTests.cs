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
            Assert.Contains("/api/admin/source-events", script, StringComparison.Ordinal);
            Assert.Contains("/api/admin/access/project-memberships", script, StringComparison.Ordinal);
            Assert.Contains("/api/admin/access/effective-preview", script, StringComparison.Ordinal);
            Assert.Contains("/api/admin/audit-exports", script, StringComparison.Ordinal);
            Assert.Contains("/api/admin/compliance/status", script, StringComparison.Ordinal);
            Assert.Contains("Audit export", script, StringComparison.Ordinal);
            Assert.Contains("Evidence Links", script, StringComparison.Ordinal);
            Assert.Contains("sourceLink", script, StringComparison.Ordinal);
            Assert.Contains("sourceRetentionClass", script, StringComparison.Ordinal);
            Assert.Contains("contentVisibilityReason", script, StringComparison.Ordinal);
            Assert.Contains("referenceType", script, StringComparison.Ordinal);
            Assert.Contains("""<option value="access">Access</option>""", html, StringComparison.Ordinal);
            Assert.Contains("""<option value="compliance">Compliance</option>""", html, StringComparison.Ordinal);
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
    public async Task Get_admin_source_events_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_source_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/api/admin/source-events");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_compliance_status_lists_payload_safe_governance_links()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_compliance_status_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareAdminMemoryFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/admin/compliance/status");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(body);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(payload.RootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(payload.RootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
            Assert.DoesNotContain(AuthorizedSourcePayload, body, StringComparison.Ordinal);
            Assert.DoesNotContain(RedactedMemoryPayload, body, StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.ActiveSourceEventId.ToString(), body, StringComparison.OrdinalIgnoreCase);

            var items = payload.RootElement.GetProperty("items")
                .EnumerateArray()
                .ToDictionary(item => item.GetProperty("id").GetString()!);

            Assert.True(items.ContainsKey("retention_report"));
            Assert.True(items.ContainsKey("legal_hold"));
            Assert.True(items.ContainsKey("erasure_replay"));
            Assert.True(items.ContainsKey("permission_drift"));
            Assert.True(items.ContainsKey("compliance_evidence_package"));

            Assert.Contains(
                items["retention_report"].GetProperty("links").EnumerateArray(),
                link => link.GetProperty("href").GetString() == "/api/admin/governance/retention-report"
                    && link.GetProperty("method").GetString() == "GET");
            Assert.Contains(
                items["permission_drift"].GetProperty("links").EnumerateArray(),
                link => link.GetProperty("href").GetString() == "/api/admin/access/permission-drift"
                    && link.GetProperty("method").GetString() == "POST");
            Assert.Contains(
                items["compliance_evidence_package"].GetProperty("metrics").EnumerateArray(),
                metric => metric.GetProperty("name").GetString() == "memorysystem_compliance_evidence_package_success");

            foreach (var item in items.Values)
            {
                Assert.True(item.GetProperty("payloadSafe").GetBoolean());
                Assert.False(item.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_source_events_lists_authorized_audit_references_without_payloads()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_source_list_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareAdminMemoryFixtureAsync(databaseConnectionString);
            var createdFrom = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
            var createdTo = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            using var request = CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/api/admin/source-events?scopeType=project&scopeId={ProjectAId}&createdFrom={createdFrom}&createdTo={createdTo}&limit=20");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(body);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(fixture.UnauthorizedSourceEventId.ToString(), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Project B admin console fact", body, StringComparison.Ordinal);
            Assert.DoesNotContain(AuthorizedSourcePayload, body, StringComparison.Ordinal);
            Assert.DoesNotContain(RedactedMemoryPayload, body, StringComparison.Ordinal);

            var events = payload.RootElement.GetProperty("events")
                .EnumerateArray()
                .ToDictionary(sourceEvent => sourceEvent.GetProperty("id").GetGuid());

            Assert.True(events.ContainsKey(fixture.ActiveSourceEventId));
            Assert.True(events.ContainsKey(fixture.ReviewSourceEventId));
            Assert.True(events.ContainsKey(fixture.ErasureSourceEventId));

            var active = events[fixture.ActiveSourceEventId];
            Assert.Equal("user_message", active.GetProperty("eventType").GetString());
            Assert.Equal($"/api/events/{fixture.ActiveSourceEventId}", active.GetProperty("sourceLink").GetString());
            Assert.Equal("project", active.GetProperty("scope").GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), active.GetProperty("scope").GetProperty("scopeId").GetString());
            Assert.Equal("standard", active.GetProperty("retentionClass").GetString());
            Assert.Equal("personal", active.GetProperty("sensitivity").GetString());
            Assert.Equal("human_approved", active.GetProperty("trustLevel").GetString());
            Assert.Equal("none", active.GetProperty("redactionStatus").GetString());

            var activePolicy = active.GetProperty("policy");
            Assert.False(activePolicy.GetProperty("sourcePayloadIncluded").GetBoolean());
            Assert.Equal("admin_list_payload_not_included", activePolicy.GetProperty("contentVisibilityReason").GetString());

            var activeReferences = active.GetProperty("references").EnumerateArray().ToArray();
            Assert.Contains(activeReferences, reference =>
                reference.GetProperty("referenceType").GetString() == "memory_fact"
                && reference.GetProperty("id").GetGuid() == fixture.ActiveFactId
                && reference.GetProperty("status").GetString() == "active");
            Assert.Contains(activeReferences, reference =>
                reference.GetProperty("referenceType").GetString() == "vault_export"
                && reference.GetProperty("id").GetGuid() == fixture.VaultExportId
                && reference.GetProperty("status").GetString() == "current");

            var reviewReferences = events[fixture.ReviewSourceEventId].GetProperty("references").EnumerateArray().ToArray();
            Assert.Contains(reviewReferences, reference =>
                reference.GetProperty("referenceType").GetString() == "memory_review"
                && reference.GetProperty("id").GetGuid() == fixture.ReviewId
                && reference.GetProperty("targetId").GetGuid() == fixture.ActiveFactId);

            var erasureReferences = events[fixture.ErasureSourceEventId].GetProperty("references").EnumerateArray().ToArray();
            Assert.Contains(erasureReferences, reference =>
                reference.GetProperty("referenceType").GetString() == "redaction"
                && reference.GetProperty("id").GetGuid() == fixture.RedactionId
                && reference.GetProperty("targetId").GetGuid() == fixture.ActiveFactId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_admin_source_events_filters_redaction_and_retention_state()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_source_filter_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareAdminMemoryFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            using var request = CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/admin/source-events?retentionClass=erasure_requested&redactionStatus=erased&trustLevel=human_approved&limit=20");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(body);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var sourceEvent = Assert.Single(payload.RootElement.GetProperty("events").EnumerateArray());
            Assert.Equal(fixture.ErasureSourceEventId, sourceEvent.GetProperty("id").GetGuid());
            Assert.Equal("erasure_requested", sourceEvent.GetProperty("retentionClass").GetString());
            Assert.Equal("erased", sourceEvent.GetProperty("redactionStatus").GetString());
            Assert.Equal("source_payload_hidden_by_retention", sourceEvent.GetProperty("policy").GetProperty("contentVisibilityReason").GetString());
            Assert.False(sourceEvent.GetProperty("policy").GetProperty("sourcePayloadIncluded").GetBoolean());
            Assert.DoesNotContain(AuthorizedSourcePayload, body, StringComparison.Ordinal);
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
        var reviewSourceEventId = Guid.NewGuid();
        var erasureSourceEventId = Guid.NewGuid();
        await InsertProjectSourceEventAsync(connectionString, activeSourceEventId, ProjectAId, OrgAId, "personal");
        await InsertProjectSourceEventAsync(connectionString, redactedSourceEventId, ProjectAId, OrgAId, "none");
        await InsertProjectSourceEventAsync(connectionString, unauthorizedSourceEventId, ProjectBId, OrgBId, "none");
        await InsertProjectSourceEventAsync(connectionString, reviewSourceEventId, ProjectAId, OrgAId, "none", retentionClass: "audit");
        await InsertProjectSourceEventAsync(
            connectionString,
            erasureSourceEventId,
            ProjectAId,
            OrgAId,
            "secret",
            retentionClass: "erasure_requested",
            redactionStatus: "erased");
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
        var reviewId = await InsertMemoryReviewAsync(connectionString, activeFact.Id, reviewSourceEventId);
        var vaultExportId = await InsertVaultExportAsync(connectionString, activeFact.Id, activeSourceEventId);
        var redactionId = await InsertRedactionAsync(connectionString, activeFact.Id, erasureSourceEventId);

        return new AdminMemoryFixture(
            activeFact.Id,
            redactedFact.Id,
            unauthorizedFact.Id,
            activeSourceEventId,
            unauthorizedSourceEventId,
            reviewSourceEventId,
            erasureSourceEventId,
            reviewId,
            vaultExportId,
            redactionId);
    }

    private static async Task InsertProjectSourceEventAsync(
        string connectionString,
        Guid eventId,
        Guid projectId,
        Guid orgId,
        string sensitivity,
        string retentionClass = "standard",
        string redactionStatus = "none")
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
            sensitivity: sensitivity,
            retentionClass: retentionClass,
            redactionStatus: redactionStatus);
    }

    private static async Task<Guid> InsertMemoryReviewAsync(
        string connectionString,
        Guid memoryFactId,
        Guid sourceEventId)
    {
        var reviewId = Guid.NewGuid();
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
                'review notes stay out of admin source event lists',
                @source_event_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("review_id", reviewId);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("reviewer_id", PrincipalId);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync();
        return reviewId;
    }

    private static async Task<Guid> InsertVaultExportAsync(
        string connectionString,
        Guid memoryFactId,
        Guid sourceEventId)
    {
        var exportId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO vault_exports (
                id,
                memory_fact_id,
                export_type,
                export_path,
                source_event_id,
                status
            )
            VALUES (
                @export_id,
                @memory_fact_id,
                'obsidian_markdown',
                '10 Decisions/admin-source-boundary.md',
                @source_event_id,
                'current'
            );
            """,
            connection);
        command.Parameters.AddWithValue("export_id", exportId);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync();
        return exportId;
    }

    private static async Task<Guid> InsertRedactionAsync(
        string connectionString,
        Guid targetId,
        Guid sourceEventId)
    {
        var redactionId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_redactions (
                id,
                target_type,
                target_id,
                redaction_type,
                reason,
                requested_by_principal_id,
                source_event_id
            )
            VALUES (
                @redaction_id,
                'memory_fact',
                @target_id,
                'redact',
                'admin source event browser test redaction',
                @requested_by,
                @source_event_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("redaction_id", redactionId);
        command.Parameters.AddWithValue("target_id", targetId);
        command.Parameters.AddWithValue("requested_by", PrincipalId);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync();
        return redactionId;
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
        Guid ActiveSourceEventId,
        Guid UnauthorizedSourceEventId,
        Guid ReviewSourceEventId,
        Guid ErasureSourceEventId,
        Guid ReviewId,
        Guid VaultExportId,
        Guid RedactionId);
}
