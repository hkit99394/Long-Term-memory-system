using System.Net;
using System.Text;
using System.Text.Json;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Worker;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed partial class ApiMemoryProposalTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestPrincipalId = "11111111-1111-4111-8111-111111111111";
    private const string OtherPrincipalId = "22222222-2222-4222-8222-222222222222";
    private const string AgentPrincipalId = "99999999-9999-4999-8999-999999999999";
    private const string TestOrgId = "33333333-3333-4333-8333-333333333333";
    private const string TestProjectId = "44444444-4444-4444-8444-444444444444";
    private const string TestEmbeddingModel = "memory-test-deterministic-v1";
    private const int TestEmbeddingDimension = 12;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid SourceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid OtherSourceEventId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid ProjectSourceEventId = Guid.Parse("88888888-8888-4888-8888-888888888888");
    private static readonly Guid LegacyAgentSourceEventId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    private static string CreateProposalBody(
        Guid? sourceEventId = null,
        decimal? confidence = 0.95m,
        string memoryType = "preference",
        string scopeType = "user",
        string? scopeId = null,
        string? namespaceValue = null,
        string visibility = "private",
        string trustLevel = "user_scoped",
        string sensitivity = "none",
        string subject = "technical planning format",
        string predicate = "prefers",
        string objectValue = "concise decision logs",
        bool includeSourceEventId = true,
        bool includeConfidence = true,
        string? roleId = null,
        Guid? baseMemoryFactId = null)
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
            ["subject"] = subject,
            ["predicate"] = predicate,
            ["object"] = objectValue,
            ["trustLevel"] = trustLevel,
            ["sensitivity"] = sensitivity
        };

        if (includeConfidence)
        {
            body["confidence"] = confidence;
        }

        if (includeSourceEventId)
        {
            body["sourceEventId"] = resolvedSourceEventId;
        }

        if (!string.IsNullOrWhiteSpace(roleId))
        {
            body["roleId"] = roleId;
        }

        if (baseMemoryFactId.HasValue)
        {
            body["baseMemoryFactId"] = baseMemoryFactId.Value;
        }

        return JsonSerializer.Serialize(body, JsonOptions);
    }

    private static string CreateProjectProposalBody(decimal? confidence = 0.95m)
    {
        return CreateProposalBody(
            sourceEventId: ProjectSourceEventId,
            confidence: confidence,
            memoryType: "decision",
            scopeType: "project",
            scopeId: TestProjectId,
            namespaceValue: $"/project/{TestProjectId}/decisions",
            visibility: "project_shared");
    }

    private static string CreateProjectRoleLensProposalBody(
        Guid baseMemoryFactId,
        string roleId = "cto")
    {
        return CreateProposalBody(
            sourceEventId: ProjectSourceEventId,
            memoryType: "project_role_lens",
            scopeType: "project",
            scopeId: TestProjectId,
            namespaceValue: $"/project/{TestProjectId}/role/cto/lens",
            visibility: "project_shared",
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize reversible rollout checkpoints",
            roleId: roleId,
            baseMemoryFactId: baseMemoryFactId);
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

    private static MemoryIndexOutboxJobHandler CreateMemoryIndexHandler(NpgsqlDataSource dataSource)
    {
        var embeddingOptions = Options.Create(new MemoryEmbeddingOptions
        {
            Model = TestEmbeddingModel,
            Dimension = TestEmbeddingDimension
        });

        return new MemoryIndexOutboxJobHandler(
            dataSource,
            new DeterministicMemoryEmbeddingProvider(embeddingOptions),
            new PostgresMemoryChunkEmbeddingStore(dataSource),
            NullLogger<MemoryIndexOutboxJobHandler>.Instance);
    }

    private static async Task PrepareDatabaseAsync(
        string connectionString,
        string sourceEventTrustLevel = "user_scoped",
        string sourceEventSensitivity = "none",
        string sourceEventRetentionClass = "standard",
        string sourceEventRedactionStatus = "none")
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, Guid.Parse(TestPrincipalId));
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            SourceEventId,
            Guid.Parse(TestPrincipalId),
            scopeType: "user",
            scopeId: TestPrincipalId,
            trustLevel: sourceEventTrustLevel,
            sensitivity: sourceEventSensitivity,
            retentionClass: sourceEventRetentionClass,
            redactionStatus: sourceEventRedactionStatus);
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

    private static async Task<int> CountRoleMemoryLensesAsync(string connectionString)
    {
        return await CountRowsAsync(connectionString, "role_memory_lenses");
    }

    private static async Task<int> CountMemoryEmbeddingsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM memory_embeddings
            WHERE embedding_model = @embedding_model;
            """,
            connection);
        command.Parameters.AddWithValue("embedding_model", TestEmbeddingModel);

        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    private static async Task SetMemoryFactStatusAsync(
        string connectionString,
        Guid memoryId,
        string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE memory_facts
            SET status = @status
            WHERE id = @memory_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_id", memoryId);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task ReplaceOutboxPayloadAsync(
        string connectionString,
        Guid aggregateId,
        string payload)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE outbox_jobs
            SET payload = @payload
            WHERE aggregate_id = @aggregate_id;
            """,
            connection);
        command.Parameters.Add("payload", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = payload;
        command.Parameters.AddWithValue("aggregate_id", aggregateId);

        await command.ExecuteNonQueryAsync();
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
                confidence,
                trust_level,
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
            reader.GetDecimal(12),
            reader.GetString(13),
            reader.GetGuid(14),
            reader.GetGuid(15));
    }

    private static async Task<MemoryChunkState> ReadMemoryChunkAsync(
        string connectionString,
        Guid sourceId,
        string sourceType = MemoryIndexOutboxJobContract.AggregateType)
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
            WHERE source_type = @source_type
                AND source_id = @source_id;
            """,
            connection);
        command.Parameters.AddWithValue("source_type", sourceType);
        command.Parameters.AddWithValue("source_id", sourceId);

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

    private static async Task<MemoryEmbeddingState> ReadMemoryEmbeddingAsync(
        string connectionString,
        Guid sourceId,
        string sourceType = MemoryIndexOutboxJobContract.AggregateType)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                embedding.embedding_model,
                embedding.embedding_dimension,
                vector_dims(embedding.embedding),
                embedding.embedding::text
            FROM memory_chunks AS chunk
            JOIN memory_embeddings AS embedding ON embedding.chunk_id = chunk.id
            WHERE chunk.source_type = @source_type
                AND chunk.source_id = @source_id
                AND embedding.embedding_model = @embedding_model;
            """,
            connection);
        command.Parameters.AddWithValue("source_type", sourceType);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("embedding_model", TestEmbeddingModel);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new MemoryEmbeddingState(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3));
    }

    private static async Task<RoleMemoryLensState> ReadRoleMemoryLensAsync(
        string connectionString,
        Guid roleMemoryLensId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                role_id,
                scope_type,
                scope_id,
                org_id,
                project_id,
                base_memory_fact_id,
                interpretation,
                confidence,
                status,
                source_event_id,
                proposed_by_principal_id
            FROM role_memory_lenses
            WHERE id = @role_memory_lens_id;
            """,
            connection);
        command.Parameters.AddWithValue("role_memory_lens_id", roleMemoryLensId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new RoleMemoryLensState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.GetGuid(5),
            reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetString(8),
            reader.GetGuid(9),
            reader.GetGuid(10));
    }

    private static async Task<OutboxJobState> ReadOutboxJobAsync(
        string connectionString,
        Guid aggregateId,
        string aggregateType = MemoryIndexOutboxJobContract.AggregateType)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT job_type, aggregate_type, aggregate_id, status
            FROM outbox_jobs
            WHERE aggregate_type = @aggregate_type
                AND aggregate_id = @aggregate_id;
            """,
            connection);
        command.Parameters.AddWithValue("aggregate_type", aggregateType);
        command.Parameters.AddWithValue("aggregate_id", aggregateId);

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

    private static async Task<IdempotencyRecordState> ReadIdempotencyRecordByKeyAsync(
        string connectionString,
        string idempotencyKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT endpoint, idempotency_key, status, response_status, resource_type, resource_id
            FROM api_idempotency_keys
            WHERE idempotency_key = @idempotency_key;
            """,
            connection);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new IdempotencyRecordState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5));
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
        decimal Confidence,
        string TrustLevel,
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

    private sealed record MemoryEmbeddingState(
        string Model,
        int Dimension,
        int VectorDimensions,
        string VectorLiteral);

    private sealed record RoleMemoryLensState(
        string RoleId,
        string ScopeType,
        string ScopeId,
        Guid? OrgId,
        Guid? ProjectId,
        Guid BaseMemoryFactId,
        string Interpretation,
        decimal Confidence,
        string Status,
        Guid SourceEventId,
        Guid ProposedByPrincipalId);

    private sealed record OutboxJobState(
        string JobType,
        string AggregateType,
        Guid AggregateId,
        string Status);
}
