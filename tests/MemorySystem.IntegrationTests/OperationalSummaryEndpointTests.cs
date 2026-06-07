using System.Net;
using System.Text.Json;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryFacts;
using MemorySystem.Infrastructure.Workers;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class OperationalSummaryEndpointTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_operations_summary_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_operations_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/api/operations/summary");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_operations_summary_returns_private_alpha_operator_readout()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_operations_summary_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);
            await InsertWorkerHeartbeatAsync(databaseConnectionString, WorkerHeartbeatStatuses.Running);
            await InsertOutboxJobAsync(databaseConnectionString, "pending");
            await InsertOutboxJobAsync(databaseConnectionString, "pending", attempts: 1, lastError: "embedding provider throttled");
            await InsertOutboxJobAsync(databaseConnectionString, "dead_letter");
            var memoryFactId = await InsertReviewedMemoryFixtureAsync(databaseConnectionString);
            var activeMemoryFactId = await InsertMemoryFixtureAsync(
                databaseConnectionString,
                "memory quality duplicate",
                "tracks",
                "source-linked quality metrics",
                MemoryFactStatuses.Active);
            await InsertMemoryFixtureAsync(
                databaseConnectionString,
                "memory quality duplicate",
                "tracks",
                "source-linked quality metrics",
                MemoryFactStatuses.Superseded);
            await InsertMemoryFixtureAsync(
                databaseConnectionString,
                "memory quality distinct",
                "tracks",
                "active source-link coverage",
                MemoryFactStatuses.Active);
            await InsertPendingReviewAsync(databaseConnectionString, memoryFactId);
            await InsertStaleVaultExportAsync(databaseConnectionString, memoryFactId);
            await InsertRetrievalFeedbackAsync(databaseConnectionString, "useful", DateTimeOffset.UtcNow.AddMinutes(-30));
            await InsertRetrievalFeedbackAsync(databaseConnectionString, "useful", DateTimeOffset.UtcNow.AddMinutes(-20));
            await InsertRetrievalFeedbackAsync(
                databaseConnectionString,
                "stale",
                DateTimeOffset.UtcNow.AddMinutes(-10),
                activeMemoryFactId);
            await InsertRetrievalFeedbackAsync(databaseConnectionString, "missing", DateTimeOffset.UtcNow.AddMinutes(-5));
            await InsertRetrievalFeedbackAsync(databaseConnectionString, "useful", DateTimeOffset.UtcNow.AddDays(-2));

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/operations/summary");
            request.Headers.Add("X-Api-Key", TestApiKey);

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(body);
            var root = payload.RootElement;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("degraded", root.GetProperty("status").GetString());
            Assert.Equal("reachable", root.GetProperty("api").GetProperty("status").GetString());

            var worker = root.GetProperty("worker");
            Assert.True(worker.GetProperty("observed").GetBoolean());
            Assert.Equal(WorkerHeartbeatStatuses.Running, worker.GetProperty("status").GetString());
            Assert.False(worker.GetProperty("stale").GetBoolean());

            var outbox = root.GetProperty("outbox");
            Assert.Equal(2, outbox.GetProperty("readyPending").GetInt64());
            Assert.Equal(1, outbox.GetProperty("deadLetter").GetInt64());
            Assert.Equal(1, outbox.GetProperty("retryingFailed").GetInt64());

            Assert.Equal(1, root.GetProperty("reviews").GetProperty("pending").GetInt64());
            Assert.Equal(1, root.GetProperty("vaultExports").GetProperty("stale").GetInt64());

            var retrievalFeedback = root.GetProperty("retrievalFeedback");
            Assert.Equal(4, retrievalFeedback.GetProperty("total").GetInt64());
            Assert.Equal(24.0d, retrievalFeedback.GetProperty("windowHours").GetDouble(), precision: 3);

            var feedbackByType = retrievalFeedback.GetProperty("byType")
                .EnumerateArray()
                .ToDictionary(item => item.GetProperty("feedbackType").GetString()!);

            Assert.Equal(2, feedbackByType["useful"].GetProperty("count").GetInt64());
            Assert.Equal(0.5m, feedbackByType["useful"].GetProperty("share").GetDecimal());
            Assert.True(feedbackByType["useful"].GetProperty("perHour").GetDouble() > 0);
            Assert.Equal(1, feedbackByType["stale"].GetProperty("count").GetInt64());
            Assert.Equal(1, feedbackByType["missing"].GetProperty("count").GetInt64());
            Assert.Equal(0, feedbackByType["noisy"].GetProperty("count").GetInt64());

            var memoryQuality = root.GetProperty("memoryQuality");
            Assert.Equal(4, memoryQuality.GetProperty("durableMemoryItems").GetInt64());
            Assert.Equal(2, memoryQuality.GetProperty("activeMemoryItems").GetInt64());
            Assert.Equal(2, memoryQuality.GetProperty("sourceLinkedActiveMemoryItems").GetInt64());
            Assert.Equal(1.0m, memoryQuality.GetProperty("sourceLinkCoverage").GetDecimal());
            Assert.Equal(1, memoryQuality.GetProperty("staleMemoryItems").GetInt64());
            Assert.Equal(0.5m, memoryQuality.GetProperty("staleMemoryRate").GetDecimal());
            Assert.Equal(2, memoryQuality.GetProperty("usefulFeedbackTotal").GetInt64());
            Assert.Equal(0.5m, memoryQuality.GetProperty("usefulFeedbackRate").GetDecimal());
            Assert.Equal(1, memoryQuality.GetProperty("missingMemoryReports").GetInt64());
            Assert.True(memoryQuality.GetProperty("missingMemoryReportsPerHour").GetDouble() > 0);
            Assert.Equal(0, memoryQuality.GetProperty("roleBoundaryMisses").GetInt64());
            Assert.Equal(1, memoryQuality.GetProperty("duplicateCandidateGroups").GetInt64());
            Assert.Equal(2, memoryQuality.GetProperty("duplicateCandidateItems").GetInt64());
            Assert.Equal(0.5m, memoryQuality.GetProperty("duplicateRatio").GetDecimal());

            var contextProduct = root.GetProperty("contextProduct");
            Assert.Equal(0, contextProduct.GetProperty("runtime").GetProperty("packetCount").GetInt64());
            Assert.Equal(0m, contextProduct.GetProperty("runtime").GetProperty("explanationCoverage").GetDecimal());
            Assert.False(contextProduct.GetProperty("benchmark").GetProperty("observed").GetBoolean());

            var contextFeedbackByAction = contextProduct
                .GetProperty("feedbackActions")
                .GetProperty("byAction")
                .EnumerateArray()
                .ToDictionary(item => item.GetProperty("feedbackType").GetString()!);
            Assert.Equal(2, contextFeedbackByAction["useful"].GetProperty("count").GetInt64());
            Assert.Equal(0.5m, contextFeedbackByAction["useful"].GetProperty("share").GetDecimal());
            Assert.Equal(1, contextFeedbackByAction["missing"].GetProperty("count").GetInt64());

            var embeddingFailures = root.GetProperty("embeddingFailures");
            Assert.Equal(1, embeddingFailures.GetProperty("retryingFailed").GetInt64());
            Assert.Equal(1, embeddingFailures.GetProperty("deadLetter").GetInt64());
            Assert.Equal(0, embeddingFailures.GetProperty("failed").GetInt64());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_operations_metrics_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_operations_metrics_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/api/operations/metrics");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_operations_metrics_exports_alert_inputs()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_operations_metrics_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);
            await InsertWorkerHeartbeatAsync(databaseConnectionString, WorkerHeartbeatStatuses.Running);
            await InsertOutboxJobAsync(databaseConnectionString, "pending");
            await InsertOutboxJobAsync(databaseConnectionString, "pending", attempts: 1, lastError: "embedding provider throttled");
            await InsertOutboxJobAsync(databaseConnectionString, "dead_letter");
            var activeMemoryFactId = await InsertMemoryFixtureAsync(
                databaseConnectionString,
                "memory quality metric",
                "exports",
                "prometheus metric lines",
                MemoryFactStatuses.Active);
            await InsertMemoryFixtureAsync(
                databaseConnectionString,
                "memory quality metric",
                "exports",
                "prometheus metric lines",
                MemoryFactStatuses.Superseded);
            await InsertRetrievalFeedbackAsync(databaseConnectionString, "useful", DateTimeOffset.UtcNow.AddMinutes(-10));
            await InsertRetrievalFeedbackAsync(
                databaseConnectionString,
                "stale",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                activeMemoryFactId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            using var summaryRequest = CreateAuthenticatedRequest("/api/operations/summary");
            using var summaryResponse = await client.SendAsync(summaryRequest);

            Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);

            using var metricsRequest = CreateAuthenticatedRequest("/api/operations/metrics");
            using var response = await client.SendAsync(metricsRequest);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("version=0.0.4", response.Content.Headers.ContentType?.ToString(), StringComparison.Ordinal);
            Assert.Contains("# TYPE memorysystem_api_requests_total counter", body, StringComparison.Ordinal);
            Assert.Contains(
                "memorysystem_api_requests_total{method=\"GET\",route=\"/api/operations/summary\",status_code=\"200\"} 1",
                body,
                StringComparison.Ordinal);
            Assert.Contains("memorysystem_health_ready 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_health_check_status{check=\"outbox\",status=\"degraded\"} 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_health_check_status{check=\"postgres\",status=\"healthy\"} 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_outbox_ready_pending 2", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_outbox_dead_letter 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_worker_heartbeat_observed{worker_type=\"outbox\"} 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_worker_heartbeat_stale{worker_type=\"outbox\"} 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_retrieval_feedback_total{window=\"24h\"} 2", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_retrieval_feedback_type_total{feedback_type=\"useful\",window=\"24h\"} 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_durable_items 2", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_active_items 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_source_linked_active_items 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_source_link_coverage 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_stale_memory_items{window=\"24h\"} 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_stale_memory_rate{window=\"24h\"} 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_useful_feedback_total{window=\"24h\"} 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_useful_feedback_rate{window=\"24h\"} 0.5", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_missing_memory_reports_total{window=\"24h\"} 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_role_boundary_misses_total 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_duplicate_candidate_groups 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_duplicate_candidate_items 2", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_memory_quality_duplicate_ratio 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_packet_total 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_explanation_coverage 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_exclusion_summary_total{reason=\"not_authorized\",count_disclosure=\"withheld\"} 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_feedback_action_total{feedback_type=\"useful\",window=\"24h\"} 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_feedback_action_share{feedback_type=\"useful\",window=\"24h\"} 0.5", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_review_open_total{feedback_type=\"stale\",created=\"true\"} 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_ranking_signal_applied_total{signal=\"feedback_adjustment_negative\"} 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_benchmark_observed 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_benchmark_delta{metric=\"feedbackAdjustmentDelta\"} 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_benchmark_read_error{reason=\"invalid_json\"} 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_embedding_provider_ready 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_embedding_outbox_retrying_failed 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_embedding_outbox_dead_letter 1", body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_operations_metrics_flags_invalid_context_product_benchmark_artifact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_operations_metrics_benchmark_error_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);
        var benchmarkPath = Path.Combine(
            Path.GetTempPath(),
            $"memorysystem-context-product-invalid-benchmark-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(benchmarkPath, "{ invalid json");
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString, benchmarkPath);
            using var client = factory.CreateClient();
            using var metricsRequest = CreateAuthenticatedRequest("/api/operations/metrics");

            using var response = await client.SendAsync(metricsRequest);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("memorysystem_context_product_benchmark_observed 0", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_benchmark_read_error{reason=\"invalid_json\"} 1", body, StringComparison.Ordinal);
            Assert.Contains("memorysystem_context_product_benchmark_read_error{reason=\"io_error\"} 0", body, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(benchmarkPath))
            {
                File.Delete(benchmarkPath);
            }

            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return CreateFactory(postgresConnectionString, contextProductBenchmarkLatestResultPath: null);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string postgresConnectionString,
        string? contextProductBenchmarkLatestResultPath)
    {
        return MemorySystemApiTestFactory.Create(
            postgresConnectionString,
            TestApiKey,
            PrincipalId.ToString(),
            contextProductBenchmarkLatestResultPath: contextProductBenchmarkLatestResultPath);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Api-Key", TestApiKey);

        return request;
    }

    private static async Task<Guid> InsertReviewedMemoryFixtureAsync(string connectionString)
    {
        return await InsertMemoryFixtureAsync(
            connectionString,
            "private alpha operator readout",
            "shows",
            "worker, outbox, review, and vault export state",
            MemoryFactStatuses.Tentative);
    }

    private static async Task<Guid> InsertMemoryFixtureAsync(
        string connectionString,
        string subject,
        string predicate,
        string @object,
        string status)
    {
        var sourceEventId = Guid.NewGuid();
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            sourceEventId,
            PrincipalId,
            "global",
            "global",
            trustLevel: "human_approved");

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
                @id,
                'global',
                'global',
                '/global/decisions',
                'decision',
                'system',
                @subject,
                @predicate,
                @object,
                0.950,
                'human_approved',
                @status,
                @source_event_id,
                @proposed_by_principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", memoryFactId);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("predicate", predicate);
        command.Parameters.AddWithValue("object", @object);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("proposed_by_principal_id", PrincipalId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }

    private static async Task InsertPendingReviewAsync(string connectionString, Guid memoryFactId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var reviewEventId = Guid.NewGuid();
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            reviewEventId,
            PrincipalId,
            "global",
            "global",
            trustLevel: "human_approved");

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_reviews (
                id,
                memory_fact_id,
                review_status,
                source_event_id,
                notes
            )
            VALUES (
                @id,
                @memory_fact_id,
                'pending',
                @source_event_id,
                'Private alpha operator readout fixture.'
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("source_event_id", reviewEventId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertRetrievalFeedbackAsync(
        string connectionString,
        string feedbackType,
        DateTimeOffset createdAt,
        Guid? sourceId = null,
        string sourceType = "memory_fact")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var requiresSource = feedbackType is "useful" or "stale" or "wrong" or "sensitive" or "over_broad" or "noisy";
        var feedbackSourceId = requiresSource
            ? sourceId.GetValueOrDefault(Guid.NewGuid())
            : (Guid?)null;

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_retrieval_feedback (
                id,
                principal_id,
                retrieval_mode,
                query_hash,
                target_scope_type,
                target_scope_id,
                source_type,
                source_id,
                feedback_type,
                created_at
            )
            VALUES (
                @id,
                @principal_id,
                'context_packet',
                @query_hash,
                'project',
                '33333333-3333-4333-8333-333333333333',
                @source_type,
                @source_id,
                @feedback_type,
                @created_at
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("principal_id", PrincipalId);
        command.Parameters.AddWithValue("query_hash", $"sha256:{Guid.NewGuid():N}");
        command.Parameters.Add("source_type", NpgsqlDbType.Text).Value =
            requiresSource ? sourceType : DBNull.Value;
        command.Parameters.Add("source_id", NpgsqlDbType.Uuid).Value =
            feedbackSourceId.HasValue ? feedbackSourceId.Value : DBNull.Value;
        command.Parameters.AddWithValue("feedback_type", feedbackType);
        command.Parameters.AddWithValue("created_at", createdAt);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertStaleVaultExportAsync(string connectionString, Guid memoryFactId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sourceEventId = Guid.NewGuid();
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            sourceEventId,
            PrincipalId,
            "global",
            "global",
            trustLevel: "human_approved");

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO vault_exports (
                id,
                memory_fact_id,
                export_type,
                export_path,
                source_event_id,
                status,
                stale_reason,
                stale_at
            )
            VALUES (
                @id,
                @memory_fact_id,
                'obsidian_markdown',
                '10 Decisions/private-alpha.md',
                @source_event_id,
                'stale',
                'memory_expired',
                now()
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertOutboxJobAsync(
        string connectionString,
        string status,
        int attempts = 0,
        string? lastError = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var aggregateId = Guid.NewGuid();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO outbox_jobs (
                id,
                job_type,
                aggregate_type,
                aggregate_id,
                idempotency_key,
                payload,
                status,
                attempts,
                last_error
            )
            VALUES (
                @id,
                'memory.index',
                'memory_fact',
                @aggregate_id,
                @idempotency_key,
                @payload,
                @status,
                @attempts,
                @last_error
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("aggregate_id", aggregateId);
        command.Parameters.AddWithValue("idempotency_key", $"memory.index:{aggregateId:N}");
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = "{}";
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("attempts", attempts);
        command.Parameters.Add("last_error", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(lastError) ? DBNull.Value : lastError;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertWorkerHeartbeatAsync(
        string connectionString,
        string status,
        string workerId = "operations-test-worker")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var now = DateTimeOffset.UtcNow;
        var lastSuccessAt = string.Equals(status, WorkerHeartbeatStatuses.Running, StringComparison.Ordinal)
            ? now
            : (DateTimeOffset?)null;

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO worker_heartbeats (
                worker_type,
                worker_id,
                status,
                last_seen_at,
                last_success_at,
                updated_at
            )
            VALUES (
                @worker_type,
                @worker_id,
                @status,
                @last_seen_at,
                @last_success_at,
                @updated_at
            );
            """,
            connection);
        command.Parameters.AddWithValue("worker_type", WorkerHeartbeatTypes.Outbox);
        command.Parameters.AddWithValue("worker_id", workerId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("last_seen_at", now);
        command.Parameters.AddWithValue("last_success_at", lastSuccessAt.HasValue ? lastSuccessAt.Value : DBNull.Value);
        command.Parameters.AddWithValue("updated_at", now);

        await command.ExecuteNonQueryAsync();
    }
}
