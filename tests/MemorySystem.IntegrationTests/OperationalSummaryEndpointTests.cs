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
            await InsertPendingReviewAsync(databaseConnectionString, memoryFactId);
            await InsertStaleVaultExportAsync(databaseConnectionString, memoryFactId);

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

    private static async Task<Guid> InsertReviewedMemoryFixtureAsync(string connectionString)
    {
        var sourceEventId = Guid.NewGuid();
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            sourceEventId,
            PrincipalId,
            "global",
            "global",
            trustLevel: "human_approved");

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new PostgresMemoryFactRepository(dataSource);
        var memory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("global", "global"),
            "/global/decisions",
            "decision",
            "system",
            "private alpha operator readout",
            "shows",
            "worker, outbox, review, and vault export state",
            0.950m,
            sourceEventId,
            PrincipalId,
            MemoryFactStatuses.Tentative));

        return memory.Id;
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
