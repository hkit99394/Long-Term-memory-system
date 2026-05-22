using System.Net;
using System.Text;
using System.Text.Json;
using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Worker;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiMemoryProposalTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestPrincipalId = "11111111-1111-4111-8111-111111111111";
    private const string OtherPrincipalId = "22222222-2222-4222-8222-222222222222";
    private const string TestOrgId = "33333333-3333-4333-8333-333333333333";
    private const string TestProjectId = "44444444-4444-4444-8444-444444444444";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid SourceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid OtherSourceEventId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid ProjectSourceEventId = Guid.Parse("88888888-8888-4888-8888-888888888888");

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_stored_decision_for_supported_durable_candidate()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_stored_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(client, "proposal-stored-key", CreateProposalBody());
            var memoryId = payload.GetProperty("memoryId").GetGuid();
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);
            var memoryFact = await ReadMemoryFactAsync(databaseConnectionString, memoryId);
            var memoryChunk = await ReadMemoryChunkAsync(databaseConnectionString, memoryId);
            var outboxJob = await ReadOutboxJobAsync(databaseConnectionString, memoryId);

            Assert.Equal("stored", payload.GetProperty("decision").GetString());
            Assert.Equal(SourceEventId, payload.GetProperty("sourceEventId").GetGuid());
            Assert.Equal("The proposal was stored as durable memory.", payload.GetProperty("reason").GetString());

            Assert.Equal("user", memoryFact.ScopeType);
            Assert.Equal(TestPrincipalId, memoryFact.ScopeId);
            Assert.Equal($"/user/{TestPrincipalId}/preferences", memoryFact.Namespace);
            Assert.Equal(Guid.Parse(TestPrincipalId), memoryFact.UserPrincipalId);
            Assert.Equal("preference", memoryFact.MemoryType);
            Assert.Equal("technical planning format", memoryFact.Subject);
            Assert.Equal("prefers", memoryFact.Predicate);
            Assert.Equal("concise decision logs", memoryFact.Object);
            Assert.Equal(SourceEventId, memoryFact.SourceEventId);
            Assert.Equal(Guid.Parse(TestPrincipalId), memoryFact.ProposedByPrincipalId);

            Assert.Equal(memoryId, memoryChunk.SourceId);
            Assert.Equal("memory_fact", memoryChunk.SourceType);
            Assert.Equal(memoryFact.Namespace, memoryChunk.Namespace);
            Assert.Equal(memoryFact.ScopeType, memoryChunk.ScopeType);
            Assert.Equal(memoryFact.ScopeId, memoryChunk.ScopeId);
            Assert.Contains("technical planning format", memoryChunk.Content, StringComparison.Ordinal);
            Assert.StartsWith("sha256:", memoryChunk.ContentHash, StringComparison.Ordinal);
            Assert.Equal("user_scoped", memoryChunk.TrustLevel);
            Assert.Equal(SourceEventId, memoryChunk.SourceEventId);

            Assert.Equal("memory.index", outboxJob.JobType);
            Assert.Equal("memory_fact", outboxJob.AggregateType);
            Assert.Equal(memoryId, outboxJob.AggregateId);
            Assert.Equal("pending", outboxJob.Status);

            Assert.Equal("POST /api/memory/proposals", idempotencyRecord.Endpoint);
            Assert.Equal("proposal-stored-key", idempotencyRecord.IdempotencyKey);
            Assert.Equal(200, idempotencyRecord.ResponseStatus);
            Assert.Equal("memory_fact", idempotencyRecord.ResourceType);
            Assert.Equal(memoryId, idempotencyRecord.ResourceId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Memory_index_outbox_handler_completes_stored_proposal_job()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_index_worker_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(client, "proposal-index-worker-key", CreateProposalBody());
            var memoryId = payload.GetProperty("memoryId").GetGuid();

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var processor = new OutboxJobProcessor(
                new PostgresOutboxJobStore(dataSource),
                [new MemoryIndexOutboxJobHandler(dataSource)],
                Options.Create(new OutboxWorkerOptions
                {
                    WorkerId = "memory-index-test-worker",
                    BatchSize = 1,
                    MaxAttempts = 2,
                    LeaseDuration = TimeSpan.FromMinutes(1),
                    HandlerTimeout = TimeSpan.FromSeconds(10),
                    RetryDelay = TimeSpan.Zero
                }),
                NullLogger<OutboxJobProcessor>.Instance);

            Assert.Equal(1, await processor.ProcessAvailableAsync());

            var outboxJob = await ReadOutboxJobAsync(databaseConnectionString, memoryId);

            Assert.Equal("completed", outboxJob.Status);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_replays_original_decision_for_same_key_and_body()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_replay_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody();

            var firstPayload = await SendProposalAsync(client, "proposal-replay-key", body);
            var secondPayload = await SendProposalAsync(client, "proposal-replay-key", body);

            Assert.Equal(firstPayload.GetProperty("decision").GetString(), secondPayload.GetProperty("decision").GetString());
            Assert.Equal(firstPayload.GetProperty("reason").GetString(), secondPayload.GetProperty("reason").GetString());
            Assert.Equal(firstPayload.GetProperty("memoryId").GetGuid(), secondPayload.GetProperty("memoryId").GetGuid());
            Assert.Equal(1, await CountIdempotencyRecordsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryFactsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryChunksAsync(databaseConnectionString));
            Assert.Equal(1, await CountOutboxJobsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_stores_project_proposal_with_membership_and_grant()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_project_access_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: true);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(
                client,
                "proposal-project-access-key",
                CreateProjectProposalBody());
            var memoryId = payload.GetProperty("memoryId").GetGuid();
            var memoryFact = await ReadMemoryFactAsync(databaseConnectionString, memoryId);

            Assert.Equal("stored", payload.GetProperty("decision").GetString());
            Assert.Equal("project", memoryFact.ScopeType);
            Assert.Equal(TestProjectId, memoryFact.ScopeId);
            Assert.Equal($"/project/{TestProjectId}/decisions", memoryFact.Namespace);
            Assert.Equal(Guid.Parse(TestOrgId), memoryFact.OrgId);
            Assert.Equal(Guid.Parse(TestProjectId), memoryFact.ProjectId);
            Assert.Null(memoryFact.UserPrincipalId);
            Assert.Equal("decision", memoryFact.MemoryType);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_forbids_project_proposal_without_membership_or_grant()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_project_forbidden_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: false);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload) = await SendProposalResponseAsync(
                client,
                "proposal-project-forbidden-key",
                CreateProjectProposalBody());
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.Forbidden, statusCode);
            Assert.Equal("Memory proposal is forbidden.", payload.GetProperty("title").GetString());
            Assert.Contains("membership access to project", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(403, idempotencyRecord.ResponseStatus);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Theory]
    [InlineData("review_required", 0.40, "preference", "user", "/user/11111111-1111-4111-8111-111111111111/preferences")]
    [InlineData("session_only", 0.95, "session_instruction", "session", "/session/session-1/instructions")]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_non_stored_decisions(
        string expectedDecision,
        decimal confidence,
        string memoryType,
        string scopeType,
        string namespaceValue)
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_{expectedDecision}_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var scopeId = scopeType == "session" ? "session-1" : TestPrincipalId;
            var sourceEventId = SourceEventId;

            if (scopeType == "session")
            {
                sourceEventId = OtherSourceEventId;
                await ApiDatabaseTestSupport.InsertSourceEventAsync(
                    databaseConnectionString,
                    sourceEventId,
                    Guid.Parse(TestPrincipalId),
                    scopeType: "session",
                    scopeId: scopeId);
            }

            var body = CreateProposalBody(
                sourceEventId: sourceEventId,
                confidence: confidence,
                memoryType: memoryType,
                scopeType: scopeType,
                scopeId: scopeId,
                namespaceValue: namespaceValue);

            var payload = await SendProposalAsync(client, $"proposal-{expectedDecision}-key", body);

            Assert.Equal(expectedDecision, payload.GetProperty("decision").GetString());
            Assert.Null(payload.GetProperty("memoryId").GetString());
            Assert.Equal(sourceEventId, payload.GetProperty("sourceEventId").GetGuid());
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_omitted_source_event()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_no_source_event_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(includeSourceEventId: false);

            var payload = await SendProposalAsync(client, "proposal-no-source-event-key", body);

            Assert.Equal("rejected", payload.GetProperty("decision").GetString());
            Assert.Contains("sourceEventId", payload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            Assert.Null(payload.GetProperty("sourceEventId").GetString());
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_unknown_source_event()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_unknown_source_event_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(sourceEventId: Guid.NewGuid());

            var payload = await SendProposalAsync(client, "proposal-unknown-source-event-key", body);

            Assert.Equal("rejected", payload.GetProperty("decision").GetString());
            Assert.Contains("source event", payload.GetProperty("reason").GetString(), StringComparison.OrdinalIgnoreCase);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_source_event_from_other_principal_scope()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_cross_source_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, Guid.Parse(OtherPrincipalId));
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                OtherSourceEventId,
                Guid.Parse(OtherPrincipalId),
                scopeType: "user",
                scopeId: OtherPrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(sourceEventId: OtherSourceEventId);

            var payload = await SendProposalAsync(client, "proposal-cross-source-event-key", body);

            Assert.Equal("rejected", payload.GetProperty("decision").GetString());
            Assert.Contains("source event", payload.GetProperty("reason").GetString(), StringComparison.OrdinalIgnoreCase);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_user_scope_that_does_not_match_authenticated_principal()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_cross_user_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(
                scopeId: OtherPrincipalId,
                namespaceValue: $"/user/{OtherPrincipalId}/preferences");

            var (statusCode, payload) = await SendProposalResponseAsync(client, "proposal-cross-user-key", body);
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory proposal is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("authenticated principal", payload.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Theory]
    [InlineData("not-a-guid", "/user/not-a-guid/preferences", "private", "user_scoped", "scopeId must be a valid GUID")]
    [InlineData("11111111-1111-4111-8111-111111111111", "/user/22222222-2222-4222-8222-222222222222/preferences", "private", "user_scoped", "namespace must start")]
    [InlineData("11111111-1111-4111-8111-111111111111", "/user/11111111-1111-4111-8111-111111111111/preferences", "public", "user_scoped", "visibility is not supported")]
    [InlineData("11111111-1111-4111-8111-111111111111", "/user/11111111-1111-4111-8111-111111111111/preferences", "private", "totally_trusted", "trustLevel is not supported")]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_bad_request_for_malformed_stored_candidates(
        string scopeId,
        string namespaceValue,
        string visibility,
        string trustLevel,
        string expectedDetail)
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_bad_request_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(
                scopeId: scopeId,
                namespaceValue: namespaceValue,
                visibility: visibility,
                trustLevel: trustLevel);

            var (statusCode, payload) = await SendProposalResponseAsync(client, $"proposal-bad-request-{Guid.NewGuid():N}", body);
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory proposal is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains(expectedDetail, payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_bad_request_for_non_json_content_type()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_non_json_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload) = await SendProposalResponseAsync(
                client,
                "proposal-non-json-key",
                CreateProposalBody(),
                "text/plain");
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory proposal is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("Content-Type", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static string CreateProposalBody(
        Guid? sourceEventId = null,
        decimal confidence = 0.95m,
        string memoryType = "preference",
        string scopeType = "user",
        string? scopeId = null,
        string? namespaceValue = null,
        string visibility = "private",
        string trustLevel = "user_scoped",
        string sensitivity = "none",
        bool includeSourceEventId = true)
    {
        var resolvedScopeId = scopeId ?? TestPrincipalId;
        var resolvedNamespace = namespaceValue ?? $"/user/{TestPrincipalId}/preferences";
        var resolvedSourceEventId = sourceEventId ?? SourceEventId;

        var body = new Dictionary<string, object?>
        {
            ["memoryType"] = memoryType,
            ["scopeType"] = scopeType,
            ["scopeId"] = resolvedScopeId,
            ["namespace"] = resolvedNamespace,
            ["visibility"] = visibility,
            ["subject"] = "technical planning format",
            ["predicate"] = "prefers",
            ["object"] = "concise decision logs",
            ["confidence"] = confidence,
            ["trustLevel"] = trustLevel,
            ["sensitivity"] = sensitivity
        };

        if (includeSourceEventId)
        {
            body["sourceEventId"] = resolvedSourceEventId;
        }

        return JsonSerializer.Serialize(body, JsonOptions);
    }

    private static string CreateProjectProposalBody()
    {
        return CreateProposalBody(
            sourceEventId: ProjectSourceEventId,
            memoryType: "decision",
            scopeType: "project",
            scopeId: TestProjectId,
            namespaceValue: $"/project/{TestProjectId}/decisions",
            visibility: "project_shared");
    }

    private static async Task<JsonElement> SendProposalAsync(
        HttpClient client,
        string idempotencyKey,
        string body)
    {
        var (statusCode, payload) = await SendProposalResponseAsync(client, idempotencyKey, body);

        Assert.Equal(HttpStatusCode.OK, statusCode);

        return payload;
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload)> SendProposalResponseAsync(
        HttpClient client,
        string idempotencyKey,
        string body,
        string mediaType = "application/json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/memory/proposals")
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        };
        request.Headers.Add("X-Api-Key", TestApiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone());
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, TestPrincipalId);
    }

    private static async Task PrepareDatabaseAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, Guid.Parse(TestPrincipalId));
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            SourceEventId,
            Guid.Parse(TestPrincipalId),
            scopeType: "user",
            scopeId: TestPrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/user/{TestPrincipalId}/preferences",
            "write",
            principalId: Guid.Parse(TestPrincipalId));
    }

    private static async Task PrepareProjectScopeAsync(
        string connectionString,
        bool includeMembershipAndGrant)
    {
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            Guid.Parse(TestOrgId),
            Guid.Parse(TestProjectId));
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectSourceEventId,
            Guid.Parse(TestPrincipalId),
            scopeType: "project",
            scopeId: TestProjectId,
            scopeOrgId: Guid.Parse(TestOrgId),
            scopeProjectId: Guid.Parse(TestProjectId));

        if (!includeMembershipAndGrant)
        {
            return;
        }

        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
            connectionString,
            Guid.Parse(TestProjectId),
            Guid.Parse(TestPrincipalId),
            "contributor");
        await ApiDatabaseTestSupport.InsertRoleAssignmentAsync(
            connectionString,
            Guid.Parse(TestPrincipalId),
            "cto",
            "project",
            Guid.Parse(TestProjectId));
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{TestProjectId}/decisions",
            "write",
            roleId: "cto");
    }

    private static async Task<int> CountIdempotencyRecordsAsync(string connectionString)
    {
        return await ApiDatabaseTestSupport.CountIdempotencyRecordsAsync(connectionString);
    }

    private static async Task<int> CountMemoryFactsAsync(string connectionString)
    {
        return await CountRowsAsync(connectionString, "memory_facts");
    }

    private static async Task<int> CountMemoryChunksAsync(string connectionString)
    {
        return await CountRowsAsync(connectionString, "memory_chunks");
    }

    private static async Task<int> CountOutboxJobsAsync(string connectionString)
    {
        return await CountRowsAsync(connectionString, "outbox_jobs");
    }

    private static async Task AssertNoDurableProposalWritesAsync(string connectionString)
    {
        Assert.Equal(0, await CountMemoryFactsAsync(connectionString));
        Assert.Equal(0, await CountMemoryChunksAsync(connectionString));
        Assert.Equal(0, await CountOutboxJobsAsync(connectionString));
    }

    private static async Task<int> CountRowsAsync(string connectionString, string tableName)
    {
        return await ApiDatabaseTestSupport.CountRowsAsync(connectionString, tableName);
    }

    private static async Task<MemoryFactState> ReadMemoryFactAsync(string connectionString, Guid memoryId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                scope_type,
                scope_id,
                namespace,
                user_principal_id,
                project_id,
                org_id,
                role_id,
                agent_principal_id,
                memory_type,
                subject,
                predicate,
                object,
                source_event_id,
                proposed_by_principal_id
            FROM memory_facts
            WHERE id = @memory_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_id", memoryId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new MemoryFactState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetGuid(12),
            reader.GetGuid(13));
    }

    private static async Task<MemoryChunkState> ReadMemoryChunkAsync(string connectionString, Guid memoryId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                source_type,
                source_id,
                namespace,
                scope_type,
                scope_id,
                content,
                content_hash,
                trust_level,
                source_event_id
            FROM memory_chunks
            WHERE source_type = 'memory_fact'
                AND source_id = @memory_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_id", memoryId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new MemoryChunkState(
            reader.GetString(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetGuid(8));
    }

    private static async Task<OutboxJobState> ReadOutboxJobAsync(string connectionString, Guid memoryId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT job_type, aggregate_type, aggregate_id, status
            FROM outbox_jobs
            WHERE aggregate_type = 'memory_fact'
                AND aggregate_id = @memory_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_id", memoryId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new OutboxJobState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetGuid(2),
            reader.GetString(3));
    }

    private static async Task<IdempotencyRecordState> ReadIdempotencyRecordAsync(string connectionString)
    {
        var record = await ApiDatabaseTestSupport.ReadSingleIdempotencySummaryAsync(connectionString);

        return new IdempotencyRecordState(
            record.Endpoint,
            record.IdempotencyKey,
            record.Status,
            record.ResponseStatus,
            record.ResourceType,
            record.ResourceId);
    }

    private sealed record IdempotencyRecordState(
        string Endpoint,
        string IdempotencyKey,
        string Status,
        int ResponseStatus,
        string? ResourceType,
        Guid? ResourceId);

    private sealed record MemoryFactState(
        string ScopeType,
        string ScopeId,
        string Namespace,
        Guid? UserPrincipalId,
        Guid? ProjectId,
        Guid? OrgId,
        string? RoleId,
        Guid? AgentPrincipalId,
        string MemoryType,
        string Subject,
        string Predicate,
        string Object,
        Guid SourceEventId,
        Guid ProposedByPrincipalId);

    private sealed record MemoryChunkState(
        string SourceType,
        Guid SourceId,
        string Namespace,
        string ScopeType,
        string ScopeId,
        string Content,
        string ContentHash,
        string TrustLevel,
        Guid SourceEventId);

    private sealed record OutboxJobState(
        string JobType,
        string AggregateType,
        Guid AggregateId,
        string Status);
}
