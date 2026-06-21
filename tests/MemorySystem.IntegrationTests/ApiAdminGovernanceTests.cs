using System.Net;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.Admin;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.Admin;
using MemorySystem.Infrastructure.MemoryFacts;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminGovernanceTests
{
    private const string TestApiKey = "test-api-key";
    private const string Namespace = "/project/33333333-3333-4333-8333-333333333333/governance";
    private const string ErasableSecret = "governance erasure secret payload";
    private const string HeldSecret = "legal hold must preserve this payload";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Admin_governance_endpoints_require_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_governance_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var reportResponse = await client.GetAsync("/api/admin/governance/retention-report");
            using var holdResponse = await client.GetAsync("/api/admin/governance/legal-holds");

            Assert.Equal(HttpStatusCode.Unauthorized, reportResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, holdResponse.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_legal_hold_creates_reports_and_release_restores_retention()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_legal_hold_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareGovernanceFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var createRequest = CreateAuthenticatedJsonRequest(
                HttpMethod.Post,
                "/api/admin/governance/legal-holds",
                "legal-hold-create-key",
                $$"""
                {
                  "eventIds": ["{{fixture.ErasableEventId}}"],
                  "reason": "customer preservation request",
                  "maxEvents": 10
                }
                """);

            using var createResponse = await client.SendAsync(createRequest);
            var createBody = await createResponse.Content.ReadAsStringAsync();
            using var createPayload = JsonDocument.Parse(createBody);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.Equal(1, createPayload.RootElement.GetProperty("matchedEvents").GetInt32());
            Assert.Equal(1, createPayload.RootElement.GetProperty("newlyHeldEvents").GetInt32());
            var holdId = createPayload.RootElement.GetProperty("holdId").GetGuid();
            Assert.Equal("legal_hold", await ReadEventRetentionClassAsync(databaseConnectionString, fixture.ErasableEventId));

            using var listRequest = CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/admin/governance/legal-holds?status=active&limit=20");
            using var listResponse = await client.SendAsync(listRequest);
            var listBody = await listResponse.Content.ReadAsStringAsync();
            using var listPayload = JsonDocument.Parse(listBody);

            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var hold = Assert.Single(listPayload.RootElement.GetProperty("holds").EnumerateArray());
            Assert.Equal(holdId, hold.GetProperty("id").GetGuid());
            Assert.Equal("active", hold.GetProperty("status").GetString());
            Assert.Equal(1, hold.GetProperty("eventCount").GetInt32());
            Assert.Equal(1, hold.GetProperty("activeEventCount").GetInt32());

            using var reportRequest = CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/api/admin/governance/retention-report?namespacePrefix={Uri.EscapeDataString(Namespace)}&limit=20");
            using var reportResponse = await client.SendAsync(reportRequest);
            var reportBody = await reportResponse.Content.ReadAsStringAsync();
            using var reportPayload = JsonDocument.Parse(reportBody);

            Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
            Assert.Contains(reportPayload.RootElement.GetProperty("rows").EnumerateArray(), row =>
                row.GetProperty("namespace").GetString() == Namespace
                && row.GetProperty("retentionClass").GetString() == "legal_hold"
                && row.GetProperty("legalHoldEvents").GetInt32() == 1);

            using var releaseRequest = CreateAuthenticatedJsonRequest(
                HttpMethod.Post,
                $"/api/admin/governance/legal-holds/{holdId}/release",
                "legal-hold-release-key",
                """
                {
                  "reason": "preservation request closed"
                }
                """);

            using var releaseResponse = await client.SendAsync(releaseRequest);
            var releaseBody = await releaseResponse.Content.ReadAsStringAsync();
            using var releasePayload = JsonDocument.Parse(releaseBody);

            Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
            Assert.Equal("released", releasePayload.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, releasePayload.RootElement.GetProperty("releasedEvents").GetInt32());
            Assert.Equal(1, releasePayload.RootElement.GetProperty("restoredEvents").GetInt32());
            Assert.Equal("standard", await ReadEventRetentionClassAsync(databaseConnectionString, fixture.ErasableEventId));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_legal_hold_release_records_authorization_denied_audit_when_release_is_forbidden()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_legal_hold_release_denied_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareGovernanceFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var createRequest = CreateAuthenticatedJsonRequest(
                HttpMethod.Post,
                "/api/admin/governance/legal-holds",
                "legal-hold-forbidden-release-create-key",
                $$"""
                {
                  "eventIds": ["{{fixture.ErasableEventId}}"],
                  "reason": "customer preservation request",
                  "maxEvents": 10
                }
                """);

            using var createResponse = await client.SendAsync(createRequest);
            var createBody = await createResponse.Content.ReadAsStringAsync();
            using var createPayload = JsonDocument.Parse(createBody);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var holdId = createPayload.RootElement.GetProperty("holdId").GetGuid();

            await DeletePrincipalNamespaceGrantAsync(databaseConnectionString, Namespace);

            using var releaseRequest = CreateAuthenticatedJsonRequest(
                HttpMethod.Post,
                $"/api/admin/governance/legal-holds/{holdId}/release",
                "legal-hold-forbidden-release-key",
                """
                {
                  "reason": "preservation request closed"
                }
                """);

            using var releaseResponse = await client.SendAsync(releaseRequest);

            Assert.Equal(HttpStatusCode.Forbidden, releaseResponse.StatusCode);
            Assert.Equal(
                1L,
                await CountLegalHoldReleaseDeniedAuditEventsAsync(databaseConnectionString, holdId));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_erasure_executes_payload_and_derived_redaction_but_skips_legal_hold()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_erasure_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareGovernanceFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var holdRequest = CreateAuthenticatedJsonRequest(
                HttpMethod.Post,
                "/api/admin/governance/legal-holds",
                "erasure-held-event-key",
                $$"""
                {
                  "eventIds": ["{{fixture.HeldEventId}}"],
                  "reason": "active legal hold",
                  "maxEvents": 10
                }
                """);
            using var holdResponse = await client.SendAsync(holdRequest);
            Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);

            using var erasureRequest = CreateAuthenticatedJsonRequest(
                HttpMethod.Post,
                "/api/admin/governance/erasures",
                "erasure-execution-key",
                $$"""
                {
                  "namespacePrefix": "{{Namespace}}",
                  "sensitivity": "secret",
                  "reason": "approved erasure request",
                  "maxEvents": 20
                }
                """);

            using var erasureResponse = await client.SendAsync(erasureRequest);
            var erasureBody = await erasureResponse.Content.ReadAsStringAsync();
            using var erasurePayload = JsonDocument.Parse(erasureBody);

            Assert.Equal(HttpStatusCode.OK, erasureResponse.StatusCode);
            Assert.Equal(2, erasurePayload.RootElement.GetProperty("matchedEvents").GetInt32());
            Assert.Equal(1, erasurePayload.RootElement.GetProperty("erasedEvents").GetInt32());
            Assert.Equal(1, erasurePayload.RootElement.GetProperty("heldEvents").GetInt32());
            Assert.Equal(1, erasurePayload.RootElement.GetProperty("redactedFacts").GetInt32());
            Assert.True(erasurePayload.RootElement.GetProperty("redactedChunks").GetInt32() >= 1);
            Assert.Equal(1, erasurePayload.RootElement.GetProperty("staleVaultExports").GetInt32());
            Assert.Equal(1, erasurePayload.RootElement.GetProperty("clearedReviewNotes").GetInt32());
            Assert.True(erasurePayload.RootElement.GetProperty("redactionRecords").GetInt32() >= 2);
            var auditEventId = erasurePayload.RootElement.GetProperty("auditEventId").GetGuid();

            var erasedEvent = await ReadEventGovernanceStateAsync(databaseConnectionString, fixture.ErasableEventId);
            Assert.Equal("erasure_requested", erasedEvent.RetentionClass);
            Assert.Equal("erased", erasedEvent.RedactionStatus);
            Assert.Equal(auditEventId, erasedEvent.RedactionEventId);
            Assert.DoesNotContain(ErasableSecret, erasedEvent.ContentJson, StringComparison.Ordinal);

            var erasedFact = await ReadFactStateAsync(databaseConnectionString, fixture.ErasableFactId);
            Assert.Equal("redacted", erasedFact.Status);
            Assert.DoesNotContain(ErasableSecret, erasedFact.ObjectValue, StringComparison.Ordinal);

            var erasedChunk = await ReadChunkStateAsync(databaseConnectionString, fixture.ErasableChunkId);
            Assert.Equal("[erased by governance workflow]", erasedChunk.Content);
            Assert.NotNull(erasedChunk.RedactedAt);

            var erasedExport = await ReadVaultExportStateAsync(databaseConnectionString, fixture.ErasableVaultExportId);
            Assert.Equal("stale", erasedExport.Status);
            Assert.Equal("source_erased", erasedExport.StaleReason);

            Assert.Null(await ReadReviewNotesAsync(databaseConnectionString, fixture.ErasableReviewId));

            var heldEvent = await ReadEventGovernanceStateAsync(databaseConnectionString, fixture.HeldEventId);
            Assert.Equal("legal_hold", heldEvent.RetentionClass);
            Assert.Equal("none", heldEvent.RedactionStatus);
            Assert.Contains(HeldSecret, heldEvent.ContentJson, StringComparison.Ordinal);
            Assert.Equal("active", (await ReadFactStateAsync(databaseConnectionString, fixture.HeldFactId)).Status);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task CreateLegalHoldAsync_rolls_back_hold_when_idempotency_completion_fails()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_legal_hold_idempotency_rollback_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareGovernanceFixtureAsync(databaseConnectionString);
            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresAdminGovernanceStore(dataSource);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.CreateLegalHoldAsync(new AdminLegalHoldCreateCommand(
                    PrincipalId,
                    Guid.NewGuid(),
                    "sha256:v2:" + new string('b', 64),
                    "customer preservation request",
                    new AdminGovernanceEventSelector(
                        PrincipalId,
                        10,
                        [fixture.ErasableEventId]))));

            Assert.Equal("standard", await ReadEventRetentionClassAsync(databaseConnectionString, fixture.ErasableEventId));
            Assert.Equal(0L, await CountLegalHoldsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ExecuteErasureAsync_rolls_back_redactions_when_idempotency_completion_fails()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_admin_erasure_idempotency_rollback_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareGovernanceFixtureAsync(databaseConnectionString);
            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresAdminGovernanceStore(dataSource);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.ExecuteErasureAsync(new AdminErasureExecutionCommand(
                    PrincipalId,
                    Guid.NewGuid(),
                    "sha256:v2:" + new string('c', 64),
                    "approved erasure request",
                    new AdminGovernanceEventSelector(
                        PrincipalId,
                        MaxEvents: 20,
                        [],
                        NamespacePrefix: Namespace,
                        Sensitivity: "secret"))));

            var erasedEvent = await ReadEventGovernanceStateAsync(databaseConnectionString, fixture.ErasableEventId);
            Assert.Equal("standard", erasedEvent.RetentionClass);
            Assert.Equal("none", erasedEvent.RedactionStatus);
            Assert.Contains(ErasableSecret, erasedEvent.ContentJson, StringComparison.Ordinal);

            var erasedFact = await ReadFactStateAsync(databaseConnectionString, fixture.ErasableFactId);
            Assert.Equal("active", erasedFact.Status);
            Assert.Contains(ErasableSecret, erasedFact.ObjectValue, StringComparison.Ordinal);
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

    private static HttpRequestMessage CreateAuthenticatedJsonRequest(
        HttpMethod method,
        string path,
        string idempotencyKey,
        string body)
    {
        var request = CreateAuthenticatedRequest(method, path);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        return request;
    }

    private static async Task<GovernanceFixture> PrepareGovernanceFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgId, ProjectId);
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, PrincipalId, "admin");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            Namespace,
            "admin",
            principalId: PrincipalId);

        var erasableEventId = Guid.NewGuid();
        var heldEventId = Guid.NewGuid();
        await InsertProjectSourceEventAsync(connectionString, erasableEventId, ErasableSecret);
        await InsertProjectSourceEventAsync(connectionString, heldEventId, HeldSecret);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new PostgresMemoryFactRepository(dataSource);
        var scope = new MemoryScopeResolution("project", ProjectId.ToString(), OrgId: OrgId, ProjectId: ProjectId);

        var erasableFact = await repository.StoreAsync(new MemoryFactWriteCommand(
            scope,
            Namespace,
            "decision",
            "project_shared",
            "Governance erasure",
            "removes",
            ErasableSecret,
            0.910m,
            erasableEventId,
            PrincipalId,
            MemoryFactStatuses.Active));
        var heldFact = await repository.StoreAsync(new MemoryFactWriteCommand(
            scope,
            Namespace,
            "decision",
            "project_shared",
            "Governance legal hold",
            "preserves",
            HeldSecret,
            0.920m,
            heldEventId,
            PrincipalId,
            MemoryFactStatuses.Active));

        var erasableChunkId = await InsertMemoryChunkAsync(connectionString, erasableFact.Id, erasableEventId, ErasableSecret);
        var erasableReviewId = await InsertMemoryReviewAsync(connectionString, erasableFact.Id, erasableEventId);
        var erasableVaultExportId = await InsertVaultExportAsync(connectionString, erasableFact.Id, erasableEventId);

        return new GovernanceFixture(
            erasableEventId,
            heldEventId,
            erasableFact.Id,
            heldFact.Id,
            erasableChunkId,
            erasableReviewId,
            erasableVaultExportId);
    }

    private static async Task InsertProjectSourceEventAsync(
        string connectionString,
        Guid eventId,
        string message)
    {
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            eventId,
            PrincipalId,
            "project",
            ProjectId.ToString(),
            scopeOrgId: OrgId,
            scopeProjectId: ProjectId,
            trustLevel: "human_approved",
            sensitivity: "secret");
        await UpdateEventContentAsync(connectionString, eventId, message);
    }

    private static async Task<Guid> InsertMemoryChunkAsync(
        string connectionString,
        Guid memoryFactId,
        Guid sourceEventId,
        string content)
    {
        var chunkId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_chunks (
                id,
                source_type,
                source_id,
                namespace,
                scope_type,
                scope_id,
                title,
                content,
                content_hash,
                trust_level,
                source_event_id
            )
            VALUES (
                @chunk_id,
                'memory_fact',
                @memory_fact_id,
                @namespace,
                'project',
                @project_id,
                'Governance chunk',
                @content,
                'sha256:chunk-test',
                'human_approved',
                @source_event_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("chunk_id", chunkId);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("namespace", Namespace);
        command.Parameters.AddWithValue("project_id", ProjectId.ToString());
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync();
        return chunkId;
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
                @notes,
                @source_event_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("review_id", reviewId);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("reviewer_id", PrincipalId);
        command.Parameters.AddWithValue("notes", ErasableSecret);
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
                '10 Governance/erasure.md',
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

    private static async Task DeletePrincipalNamespaceGrantAsync(
        string connectionString,
        string namespacePrefix)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM memory_access_grants
            WHERE principal_id = @principal_id
                AND namespace_prefix = @namespace_prefix;
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", PrincipalId);
        command.Parameters.AddWithValue("namespace_prefix", namespacePrefix);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountLegalHoldReleaseDeniedAuditEventsAsync(
        string connectionString,
        Guid holdId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM access_audit_events
            WHERE action_type = 'authorization_denied'
                AND outcome = 'denied'
                AND actor_principal_id = @principal_id
                AND resource_type = 'legal_hold'
                AND resource_id = @hold_id
                AND reason_code = 'legal_hold_release_forbidden';
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", PrincipalId);
        command.Parameters.AddWithValue("hold_id", holdId.ToString("D"));

        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Legal hold release denied audit count was not returned."));
    }

    private static async Task<long> CountLegalHoldsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM governance_legal_holds;
            """,
            connection);

        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Legal hold count was not returned."));
    }

    private static async Task<string> ReadEventRetentionClassAsync(string connectionString, Guid eventId)
    {
        var state = await ReadEventGovernanceStateAsync(connectionString, eventId);
        return state.RetentionClass;
    }

    private static async Task<EventGovernanceState> ReadEventGovernanceStateAsync(string connectionString, Guid eventId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT retention_class, redaction_status, redaction_event_id, content::text
            FROM events
            WHERE id = @event_id;
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return new EventGovernanceState(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.GetString(3));
    }

    private static async Task<FactState> ReadFactStateAsync(string connectionString, Guid factId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT status, object
            FROM memory_facts
            WHERE id = @fact_id;
            """,
            connection);
        command.Parameters.AddWithValue("fact_id", factId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return new FactState(reader.GetString(0), reader.GetString(1));
    }

    private static async Task<ChunkState> ReadChunkStateAsync(string connectionString, Guid chunkId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT content, redacted_at
            FROM memory_chunks
            WHERE id = @chunk_id;
            """,
            connection);
        command.Parameters.AddWithValue("chunk_id", chunkId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return new ChunkState(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1));
    }

    private static async Task<VaultExportState> ReadVaultExportStateAsync(string connectionString, Guid vaultExportId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT status, stale_reason
            FROM vault_exports
            WHERE id = @vault_export_id;
            """,
            connection);
        command.Parameters.AddWithValue("vault_export_id", vaultExportId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return new VaultExportState(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static async Task<string?> ReadReviewNotesAsync(string connectionString, Guid reviewId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT notes
            FROM memory_reviews
            WHERE id = @review_id;
            """,
            connection);
        command.Parameters.AddWithValue("review_id", reviewId);

        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : (string?)value;
    }

    private sealed record GovernanceFixture(
        Guid ErasableEventId,
        Guid HeldEventId,
        Guid ErasableFactId,
        Guid HeldFactId,
        Guid ErasableChunkId,
        Guid ErasableReviewId,
        Guid ErasableVaultExportId);

    private sealed record EventGovernanceState(
        string RetentionClass,
        string RedactionStatus,
        Guid? RedactionEventId,
        string ContentJson);

    private sealed record FactState(string Status, string ObjectValue);

    private sealed record ChunkState(string Content, DateTimeOffset? RedactedAt);

    private sealed record VaultExportState(string Status, string? StaleReason);
}
