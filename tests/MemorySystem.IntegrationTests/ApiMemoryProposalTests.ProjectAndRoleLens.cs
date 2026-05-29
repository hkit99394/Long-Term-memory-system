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
    [DatabaseFact]
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
            Assert.Equal("decision", payload.GetProperty("candidateKind").GetString());
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

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_stores_project_role_lens_and_worker_indexes_chunk()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_project_role_lens_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: true);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{TestProjectId}/role/cto/lens",
                "write",
                roleId: "cto");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var basePayload = await SendProposalAsync(
                client,
                "proposal-role-lens-base-key",
                CreateProjectProposalBody());
            var baseMemoryFactId = basePayload.GetProperty("memoryId").GetGuid();

            var lensPayload = await SendProposalAsync(
                client,
                "proposal-role-lens-key",
                CreateProjectRoleLensProposalBody(baseMemoryFactId));
            var roleMemoryLensId = lensPayload.GetProperty("memoryId").GetGuid();
            var roleMemoryLens = await ReadRoleMemoryLensAsync(databaseConnectionString, roleMemoryLensId);
            var memoryChunk = await ReadMemoryChunkAsync(
                databaseConnectionString,
                roleMemoryLensId,
                MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType);
            var outboxJob = await ReadOutboxJobAsync(
                databaseConnectionString,
                roleMemoryLensId,
                MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType);
            var idempotencyRecord = await ReadIdempotencyRecordByKeyAsync(
                databaseConnectionString,
                "proposal-role-lens-key");

            Assert.Equal("stored", lensPayload.GetProperty("decision").GetString());
            Assert.Equal("role_lens", lensPayload.GetProperty("candidateKind").GetString());
            Assert.Equal(ProjectSourceEventId, lensPayload.GetProperty("sourceEventId").GetGuid());
            Assert.Equal("The proposal was stored as a role memory lens.", lensPayload.GetProperty("reason").GetString());

            Assert.Equal("cto", roleMemoryLens.RoleId);
            Assert.Equal("project", roleMemoryLens.ScopeType);
            Assert.Equal(TestProjectId, roleMemoryLens.ScopeId);
            Assert.Equal(Guid.Parse(TestOrgId), roleMemoryLens.OrgId);
            Assert.Equal(Guid.Parse(TestProjectId), roleMemoryLens.ProjectId);
            Assert.Equal(baseMemoryFactId, roleMemoryLens.BaseMemoryFactId);
            Assert.Equal("prioritize reversible rollout checkpoints", roleMemoryLens.Interpretation);
            Assert.Equal("active", roleMemoryLens.Status);
            Assert.Equal(ProjectSourceEventId, roleMemoryLens.SourceEventId);
            Assert.Equal(Guid.Parse(TestPrincipalId), roleMemoryLens.ProposedByPrincipalId);

            Assert.Equal(MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType, memoryChunk.SourceType);
            Assert.Equal(roleMemoryLensId, memoryChunk.SourceId);
            Assert.Equal($"/project/{TestProjectId}/role/cto/lens", memoryChunk.Namespace);
            Assert.Equal("project", memoryChunk.ScopeType);
            Assert.Equal(TestProjectId, memoryChunk.ScopeId);
            Assert.Contains("prioritize reversible rollout checkpoints", memoryChunk.Content, StringComparison.Ordinal);
            Assert.Equal(ProjectSourceEventId, memoryChunk.SourceEventId);

            Assert.Equal(MemoryIndexOutboxJobContract.JobType, outboxJob.JobType);
            Assert.Equal(MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType, outboxJob.AggregateType);
            Assert.Equal(roleMemoryLensId, outboxJob.AggregateId);
            Assert.Equal("pending", outboxJob.Status);

            Assert.Equal("role_memory_lens", idempotencyRecord.ResourceType);
            Assert.Equal(roleMemoryLensId, idempotencyRecord.ResourceId);

            var duplicateLensPayload = await SendProposalAsync(
                client,
                "proposal-role-lens-duplicate-key",
                CreateProjectRoleLensProposalBody(baseMemoryFactId));

            Assert.Equal("stored", duplicateLensPayload.GetProperty("decision").GetString());
            Assert.Equal(roleMemoryLensId, duplicateLensPayload.GetProperty("memoryId").GetGuid());
            Assert.Contains("existing durable role memory lens", duplicateLensPayload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            Assert.Equal(1, await CountRoleMemoryLensesAsync(databaseConnectionString));
            Assert.Equal(2, await CountMemoryChunksAsync(databaseConnectionString));
            Assert.Equal(2, await CountOutboxJobsAsync(databaseConnectionString));

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var processor = new OutboxJobProcessor(
                new PostgresOutboxJobStore(dataSource),
                [CreateMemoryIndexHandler(dataSource)],
                Options.Create(new OutboxWorkerOptions
                {
                    WorkerId = "role-lens-index-test-worker",
                    BatchSize = 2,
                    MaxAttempts = 2,
                    LeaseDuration = TimeSpan.FromMinutes(1),
                    HandlerTimeout = TimeSpan.FromSeconds(10),
                    RetryDelay = TimeSpan.Zero
                }),
                NullLogger<OutboxJobProcessor>.Instance);

            Assert.Equal(2, await processor.ProcessAvailableAsync());
            outboxJob = await ReadOutboxJobAsync(
                databaseConnectionString,
                roleMemoryLensId,
                MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType);

            Assert.Equal("completed", outboxJob.Status);
            Assert.Equal(1, await CountRoleMemoryLensesAsync(databaseConnectionString));
            Assert.Equal(2, await CountMemoryEmbeddingsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_role_lens_namespace_that_does_not_match_role_id()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_role_lens_namespace_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: true);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{TestProjectId}/role/cto/lens",
                "write",
                roleId: "cto");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var basePayload = await SendProposalAsync(
                client,
                "proposal-role-lens-namespace-base-key",
                CreateProjectProposalBody());
            var baseMemoryFactId = basePayload.GetProperty("memoryId").GetGuid();

            var (statusCode, payload) = await SendProposalResponseAsync(
                client,
                "proposal-role-lens-namespace-key",
                CreateProjectRoleLensProposalBody(baseMemoryFactId, roleId: "cfo"));
            var idempotencyRecord = await ReadIdempotencyRecordByKeyAsync(
                databaseConnectionString,
                "proposal-role-lens-namespace-key");

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory proposal is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("roleId 'cfo'", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            Assert.Equal(0, await CountRoleMemoryLensesAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_bad_request_for_unknown_role_lens_base_fact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_unknown_role_lens_base_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: true);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{TestProjectId}/role/cto/lens",
                "write",
                roleId: "cto");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload) = await SendProposalResponseAsync(
                client,
                "proposal-unknown-role-lens-base-key",
                CreateProjectRoleLensProposalBody(Guid.NewGuid()));
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory proposal is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("baseMemoryFactId", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            Assert.Equal(0, await CountRoleMemoryLensesAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
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

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_forbids_unauthorized_project_review_candidate_before_broker_decision()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_project_review_forbidden_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: false);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload) = await SendProposalResponseAsync(
                client,
                "proposal-project-review-forbidden-key",
                CreateProjectProposalBody(confidence: 0.40m));
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
}
