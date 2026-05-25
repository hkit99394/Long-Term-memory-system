using System.Net;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryFacts;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiMemoryReviewTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgAId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectAId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgBId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectBId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_reviews_dashboard_serves_static_assets()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_review_dashboard_static_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var html = await client.GetStringAsync("/reviews/");
            var script = await client.GetStringAsync("/reviews/review-dashboard.js");

            Assert.Contains("Memory Reviews", html, StringComparison.Ordinal);
            Assert.Contains("/reviews/review-dashboard.js", html, StringComparison.Ordinal);
            Assert.Contains("api/reviews/pending", script, StringComparison.Ordinal);
            Assert.Contains("supersede", script, StringComparison.Ordinal);
            Assert.Contains("Idempotency-Key", script, StringComparison.Ordinal);
            Assert.Contains("actionIdempotencyKeys", script, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_reviews_pending_returns_authorized_pending_memories_with_source_links()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_pending_reviews_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PreparePendingReviewFixtureAsync(databaseConnectionString, grantReviewAccess: true);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendPendingReviewsAsync(client, limit: 10);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var review = Assert.Single(payload.GetProperty("reviews").EnumerateArray());
            Assert.Equal(fixture.ProjectAReviewId, review.GetProperty("id").GetGuid());
            Assert.Equal("pending", review.GetProperty("reviewStatus").GetString());
            Assert.Equal(JsonValueKind.Null, review.GetProperty("reviewerId").ValueKind);
            Assert.Equal("Needs human review before durable retrieval.", review.GetProperty("notes").GetString());
            Assert.Equal(fixture.ProjectAReviewEventId, review.GetProperty("sourceEventId").GetGuid());
            Assert.Equal($"/api/events/{fixture.ProjectAReviewEventId}", review.GetProperty("sourceLink").GetString());

            var memory = review.GetProperty("memory");
            Assert.Equal(fixture.ProjectAMemoryId, memory.GetProperty("id").GetGuid());
            Assert.Equal("project", memory.GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), memory.GetProperty("scopeId").GetString());
            Assert.Equal($"/project/{ProjectAId}/decisions", memory.GetProperty("namespace").GetString());
            Assert.Equal("decision", memory.GetProperty("memoryType").GetString());
            Assert.Equal("Long-Term Memory System review workflow", memory.GetProperty("subject").GetString());
            Assert.Equal("tentative", memory.GetProperty("status").GetString());
            Assert.Equal(fixture.ProjectAMemoryEventId, memory.GetProperty("sourceEventId").GetGuid());
            Assert.Equal($"/api/events/{fixture.ProjectAMemoryEventId}", memory.GetProperty("sourceLink").GetString());
            Assert.DoesNotContain(fixture.ProjectBReviewId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Project B pending review", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseTheory]
    [Trait("Category", "Database")]
    [InlineData("approve", "approved", "active", false)]
    [InlineData("reject", "rejected", "deleted", false)]
    [InlineData("edit", "approved", "active", false)]
    [InlineData("expire", "approved", "expired", false)]
    [InlineData("delete", "approved", "deleted", false)]
    [InlineData("supersede", "approved", "superseded", true)]
    public async Task Post_reviews_action_completes_review_workflow(
        string action,
        string expectedReviewStatus,
        string expectedMemoryStatus,
        bool expectsReplacement)
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_review_{action}_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PreparePendingReviewFixtureAsync(databaseConnectionString, grantReviewAccess: true);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload) = await SendReviewActionAsync(
                client,
                fixture.ProjectAReviewId,
                action,
                fixture.ProjectAReviewEventId,
                includeContent: action is "edit" or "supersede");

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Equal(action, payload.GetProperty("action").GetString());
            Assert.Equal(expectedReviewStatus, payload.GetProperty("review").GetProperty("reviewStatus").GetString());
            Assert.Equal(expectedMemoryStatus, payload.GetProperty("review").GetProperty("memory").GetProperty("status").GetString());

            var row = await ReadReviewAndMemoryStatusAsync(databaseConnectionString, fixture.ProjectAReviewId);
            Assert.Equal(expectedReviewStatus, row.ReviewStatus);
            Assert.Equal(expectedMemoryStatus, row.MemoryStatus);
            Assert.Equal(PrincipalId, row.ReviewerId);

            if (action == "edit")
            {
                Assert.Equal("Edited review subject", row.Subject);
                Assert.Equal("records", row.Predicate);
                Assert.Equal("edited review object", row.Object);
                Assert.Equal(1, await CountMemoryChunksForSourceAsync(databaseConnectionString, fixture.ProjectAMemoryId));
            }
            else if (expectsReplacement)
            {
                var replacementMemoryFactId = payload.GetProperty("replacementMemoryFactId").GetGuid();
                var replacement = await ReadMemoryStatusAsync(databaseConnectionString, replacementMemoryFactId);

                Assert.Equal("active", replacement.MemoryStatus);
                Assert.Equal("Edited review subject", replacement.Subject);
                Assert.Equal(1, await CountMemoryChunksForSourceAsync(databaseConnectionString, replacementMemoryFactId));
            }
            else if (expectedMemoryStatus == "active")
            {
                Assert.Equal(1, await CountMemoryChunksForSourceAsync(databaseConnectionString, fixture.ProjectAMemoryId));
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_reviews_action_replays_original_response_for_same_idempotency_key_and_body()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_review_replay_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PreparePendingReviewFixtureAsync(databaseConnectionString, grantReviewAccess: true);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var first = await SendReviewActionAsync(
                client,
                fixture.ProjectAReviewId,
                "supersede",
                fixture.ProjectAReviewEventId,
                includeContent: true,
                idempotencyKey: "review-supersede-replay-key");
            var second = await SendReviewActionAsync(
                client,
                fixture.ProjectAReviewId,
                "supersede",
                fixture.ProjectAReviewEventId,
                includeContent: true,
                idempotencyKey: "review-supersede-replay-key");

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(
                first.Payload.GetProperty("replacementMemoryFactId").GetGuid(),
                second.Payload.GetProperty("replacementMemoryFactId").GetGuid());
            Assert.Equal(3, await ApiDatabaseTestSupport.CountRowsAsync(databaseConnectionString, "memory_facts"));
            Assert.Equal(1, await ApiDatabaseTestSupport.CountIdempotencyRecordsAsync(databaseConnectionString));

            var idempotencyRecord = await ApiDatabaseTestSupport.ReadSingleIdempotencySummaryAsync(databaseConnectionString);
            Assert.Equal("POST /api/reviews/supersede", idempotencyRecord.Endpoint);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal("memory_review", idempotencyRecord.ResourceType);
            Assert.Equal(fixture.ProjectAReviewId, idempotencyRecord.ResourceId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_reviews_edit_updates_fact_evidence_to_review_source_event()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_review_edit_evidence_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PreparePendingReviewFixtureAsync(
                databaseConnectionString,
                grantReviewAccess: true,
                projectAReviewTrustLevel: "user_scoped");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, _) = await SendReviewActionAsync(
                client,
                fixture.ProjectAReviewId,
                "edit",
                fixture.ProjectAReviewEventId,
                includeContent: true);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var row = await ReadReviewAndMemoryStatusAsync(databaseConnectionString, fixture.ProjectAReviewId);

            Assert.Equal("approved", row.ReviewStatus);
            Assert.Equal("active", row.MemoryStatus);
            Assert.Equal("Edited review subject", row.Subject);
            Assert.Equal("user_scoped", row.TrustLevel);
            Assert.Equal(fixture.ProjectAReviewEventId, row.SourceEventId);
            Assert.Equal(PrincipalId, row.ProposedByPrincipalId);
            Assert.Equal(1, await CountMemoryChunksForSourceAsync(databaseConnectionString, fixture.ProjectAMemoryId));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_reviews_pending_hides_pending_memory_without_review_access()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_pending_reviews_access_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PreparePendingReviewFixtureAsync(databaseConnectionString, grantReviewAccess: false);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendPendingReviewsAsync(client, limit: 10);

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Empty(payload.GetProperty("reviews").EnumerateArray());
            Assert.DoesNotContain(fixture.ProjectAReviewId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(fixture.ProjectAMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_reviews_pending_returns_authorized_review_after_large_inaccessible_prefix()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_pending_reviews_authorized_sql_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);
            await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(databaseConnectionString, OrgAId, ProjectAId);
            await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(databaseConnectionString, OrgBId, ProjectBId);
            await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
                databaseConnectionString,
                ProjectAId,
                PrincipalId,
                "reviewer");
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{ProjectAId}/decisions",
                "review",
                principalId: PrincipalId);

            var projectBMemoryEventId = Guid.NewGuid();
            var projectBReviewEventId = Guid.NewGuid();
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                projectBMemoryEventId,
                PrincipalId,
                "project",
                ProjectBId.ToString(),
                scopeOrgId: OrgBId,
                scopeProjectId: ProjectBId,
                trustLevel: "human_approved");
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                projectBReviewEventId,
                PrincipalId,
                "project",
                ProjectBId.ToString(),
                scopeOrgId: OrgBId,
                scopeProjectId: ProjectBId,
                trustLevel: "human_approved");

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var repository = new PostgresMemoryFactRepository(dataSource);
            var projectBMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
                new MemoryScopeResolution("project", ProjectBId.ToString(), OrgId: OrgBId, ProjectId: ProjectBId),
                $"/project/{ProjectBId}/decisions",
                "decision",
                "project_shared",
                "Project B inaccessible review",
                "should",
                "not hide later authorized reviews",
                0.650m,
                projectBMemoryEventId,
                PrincipalId,
                MemoryFactStatuses.Tentative));

            var createdAt = DateTimeOffset.UtcNow.AddMinutes(-10);

            for (var index = 0; index < 251; index++)
            {
                await InsertPendingReviewAsync(
                    databaseConnectionString,
                    Guid.NewGuid(),
                    projectBMemory.Id,
                    projectBReviewEventId,
                    $"Inaccessible Project B review {index}",
                    createdAt.AddMilliseconds(index));
            }

            var projectAMemoryEventId = Guid.NewGuid();
            var projectAReviewEventId = Guid.NewGuid();
            var projectAReviewId = Guid.NewGuid();
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                projectAMemoryEventId,
                PrincipalId,
                "project",
                ProjectAId.ToString(),
                scopeOrgId: OrgAId,
                scopeProjectId: ProjectAId,
                trustLevel: "human_approved");
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                projectAReviewEventId,
                PrincipalId,
                "project",
                ProjectAId.ToString(),
                scopeOrgId: OrgAId,
                scopeProjectId: ProjectAId,
                trustLevel: "human_approved");
            var projectAMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
                new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
                $"/project/{ProjectAId}/decisions",
                "decision",
                "project_shared",
                "Project A authorized review",
                "must",
                "be returned without scanning inaccessible rows in application code",
                0.650m,
                projectAMemoryEventId,
                PrincipalId,
                MemoryFactStatuses.Tentative));
            await InsertPendingReviewAsync(
                databaseConnectionString,
                projectAReviewId,
                projectAMemory.Id,
                projectAReviewEventId,
                "Authorized review after inaccessible prefix.",
                createdAt.AddMinutes(1));

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendPendingReviewsAsync(client, limit: 1);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var review = Assert.Single(payload.GetProperty("reviews").EnumerateArray());
            Assert.Equal(projectAReviewId, review.GetProperty("id").GetGuid());
            Assert.DoesNotContain(projectBMemory.Id.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_reviews_pending_rejects_invalid_limit()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_pending_reviews_limit_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendPendingReviewsAsync(client, limit: 51);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Pending reviews request is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("between 1 and 50", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task<PendingReviewFixture> PreparePendingReviewFixtureAsync(
        string connectionString,
        bool grantReviewAccess,
        string projectAReviewTrustLevel = "human_approved")
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgAId, ProjectAId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgBId, ProjectBId);
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
            connectionString,
            ProjectAId,
            PrincipalId,
            grantReviewAccess ? "reviewer" : "reader");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/decisions",
            grantReviewAccess ? "review" : "read",
            principalId: PrincipalId);

        var projectAMemoryEventId = Guid.NewGuid();
        var projectAReviewEventId = Guid.NewGuid();
        var projectBMemoryEventId = Guid.NewGuid();
        var projectBReviewEventId = Guid.NewGuid();

        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            projectAMemoryEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "human_approved");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            projectAReviewEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: projectAReviewTrustLevel);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            projectBMemoryEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId,
            trustLevel: "human_approved");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            projectBReviewEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId,
            trustLevel: "human_approved");

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new PostgresMemoryFactRepository(dataSource);
        var projectAMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "Long-Term Memory System review workflow",
            "needs",
            "pending review API before dashboard work",
            0.650m,
            projectAMemoryEventId,
            PrincipalId,
            MemoryFactStatuses.Tentative));
        var projectBMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectBId.ToString(), OrgId: OrgBId, ProjectId: ProjectBId),
            $"/project/{ProjectBId}/decisions",
            "decision",
            "project_shared",
            "Project B pending review",
            "must",
            "stay hidden from Project A reviewers",
            0.650m,
            projectBMemoryEventId,
            PrincipalId,
            MemoryFactStatuses.Tentative));
        var projectAReviewId = Guid.NewGuid();
        var projectBReviewId = Guid.NewGuid();

        await InsertPendingReviewAsync(
            connectionString,
            projectAReviewId,
            projectAMemory.Id,
            projectAReviewEventId,
            "Needs human review before durable retrieval.");
        await InsertPendingReviewAsync(
            connectionString,
            projectBReviewId,
            projectBMemory.Id,
            projectBReviewEventId,
            "Project B pending review must stay hidden.");

        return new PendingReviewFixture(
            projectAReviewId,
            projectAMemory.Id,
            projectAMemoryEventId,
            projectAReviewEventId,
            projectBReviewId,
            projectBMemory.Id,
            projectBMemoryEventId,
            projectBReviewEventId);
    }

    private static async Task InsertPendingReviewAsync(
        string connectionString,
        Guid reviewId,
        Guid memoryFactId,
        Guid sourceEventId,
        string notes,
        DateTimeOffset? createdAt = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_reviews (
                id,
                memory_fact_id,
                review_status,
                notes,
                source_event_id,
                created_at,
                updated_at
            )
            VALUES (
                @review_id,
                @memory_fact_id,
                'pending',
                @notes,
                @source_event_id,
                @created_at,
                @created_at
            );
            """,
            connection);
        command.Parameters.AddWithValue("review_id", reviewId);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("notes", notes);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("created_at", createdAt ?? DateTimeOffset.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendPendingReviewsAsync(
        HttpClient client,
        int? limit = null)
    {
        var uri = "/api/reviews/pending";

        if (limit.HasValue)
        {
            uri += $"?limit={limit.Value}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload)> SendReviewActionAsync(
        HttpClient client,
        Guid reviewId,
        string action,
        Guid sourceEventId,
        bool includeContent,
        string? idempotencyKey = null)
    {
        var body = includeContent
            ? $$"""
              {
                "sourceEventId": "{{sourceEventId}}",
                "notes": "Reviewed from dashboard workflow.",
                "subject": "Edited review subject",
                "predicate": "records",
                "object": "edited review object"
              }
              """
            : $$"""
              {
                "sourceEventId": "{{sourceEventId}}",
                "notes": "Reviewed from dashboard workflow."
              }
              """;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/reviews/{reviewId}/{action}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", TestApiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? $"review-{reviewId:N}-{action}");

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone());
    }

    private static async Task<ReviewMemoryStatus> ReadReviewAndMemoryStatusAsync(
        string connectionString,
        Guid reviewId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                review.review_status,
                review.reviewer_id,
                fact.status,
                fact.subject,
                fact.predicate,
                fact.object,
                fact.trust_level,
                fact.source_event_id,
                fact.proposed_by_principal_id
            FROM memory_reviews AS review
            INNER JOIN memory_facts AS fact
                ON fact.id = review.memory_fact_id
            WHERE review.id = @review_id;
            """,
            connection);
        command.Parameters.AddWithValue("review_id", reviewId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new ReviewMemoryStatus(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetGuid(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8));
    }

    private static async Task<ReviewMemoryStatus> ReadMemoryStatusAsync(
        string connectionString,
        Guid memoryFactId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                status,
                subject,
                predicate,
                object,
                trust_level,
                source_event_id,
                proposed_by_principal_id
            FROM memory_facts
            WHERE id = @memory_fact_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new ReviewMemoryStatus(
            ReviewStatus: string.Empty,
            ReviewerId: null,
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6));
    }

    private static async Task<int> CountMemoryChunksForSourceAsync(
        string connectionString,
        Guid memoryFactId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM memory_chunks
            WHERE source_type = 'memory_fact'
                AND source_id = @memory_fact_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, PrincipalId.ToString());
    }

    private sealed record PendingReviewFixture(
        Guid ProjectAReviewId,
        Guid ProjectAMemoryId,
        Guid ProjectAMemoryEventId,
        Guid ProjectAReviewEventId,
        Guid ProjectBReviewId,
        Guid ProjectBMemoryId,
        Guid ProjectBMemoryEventId,
        Guid ProjectBReviewEventId);

    private sealed record ReviewMemoryStatus(
        string ReviewStatus,
        Guid? ReviewerId,
        string MemoryStatus,
        string Subject,
        string Predicate,
        string Object,
        string TrustLevel,
        Guid SourceEventId,
        Guid? ProposedByPrincipalId);
}
