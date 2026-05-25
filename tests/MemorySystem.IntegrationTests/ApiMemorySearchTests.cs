using System.Net;
using System.Text.Json;
using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Application.MemoryEvaluations;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.RoleMemoryLenses;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.MemoryFacts;
using MemorySystem.Infrastructure.RoleMemoryLenses;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiMemorySearchTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgAId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectAId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgBId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectBId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid ProjectAEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid ProjectBEventId = Guid.Parse("77777777-7777-4777-8777-777777777777");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_search_returns_authorized_full_text_matches_only()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_search_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var (projectAMemoryId, _) = await PrepareSearchFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendSearchAsync(client, "postgres audit retrieval");

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var results = payload.GetProperty("results").EnumerateArray().ToArray();
            var result = Assert.Single(results);

            Assert.Equal(projectAMemoryId, result.GetProperty("sourceId").GetGuid());
            Assert.Equal("memory_fact", result.GetProperty("sourceType").GetString());
            Assert.Equal("project", result.GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), result.GetProperty("scopeId").GetString());
            Assert.Equal($"/project/{ProjectAId}/decisions", result.GetProperty("namespace").GetString());
            Assert.True(result.GetProperty("rank").GetDouble() > 0);
            Assert.Contains("Project A retrieval decision", result.GetProperty("content").GetString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Project B retrieval decision", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_search_rejects_blank_query()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_search_blank_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendSearchAsync(client, " ");

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory search is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("'q' is required", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_semantic_search_returns_authorized_vector_matches_only()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_semantic_search_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var (projectATargetMemoryId, projectBMemoryId, query) =
                await PrepareSemanticSearchFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendSemanticSearchAsync(client, query, limit: 2);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var results = payload.GetProperty("results").EnumerateArray().ToArray();

            Assert.Equal(2, results.Length);
            Assert.Equal(projectATargetMemoryId, results[0].GetProperty("sourceId").GetGuid());
            Assert.Equal("memory_fact", results[0].GetProperty("sourceType").GetString());
            Assert.Equal("project", results[0].GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), results[0].GetProperty("scopeId").GetString());
            Assert.Equal($"/project/{ProjectAId}/decisions", results[0].GetProperty("namespace").GetString());
            Assert.InRange(results[0].GetProperty("rank").GetDouble(), 0.999d, 1.001d);
            Assert.DoesNotContain(projectBMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_semantic_search_rejects_blank_query()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_semantic_blank_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendSemanticSearchAsync(client, " ");

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory semantic search is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("'q' is required", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Search_endpoints_require_matching_role_assignment_for_role_specific_chunks()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_search_role_boundary_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            const string query = "role boundary sentinel";
            const string privateLensText = "must stay behind a role assignment";
            await PrepareRoleSpecificSearchFixtureWithoutAssignmentAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (fullTextStatus, fullTextPayload, fullTextBody) = await SendSearchAsync(client, query);
            var (semanticStatus, semanticPayload, semanticBody) = await SendSemanticSearchAsync(client, query);
            var (hybridStatus, hybridPayload, hybridBody) = await SendHybridSearchAsync(client, query);
            var (contextStatus, contextPayload, contextBody) = await SendContextPacketAsync(
                client,
                query,
                roleId: "cto",
                scopeType: "project",
                scopeId: ProjectAId.ToString());

            Assert.Equal(HttpStatusCode.OK, fullTextStatus);
            Assert.Empty(fullTextPayload.GetProperty("results").EnumerateArray());
            Assert.Equal(HttpStatusCode.OK, semanticStatus);
            Assert.Empty(semanticPayload.GetProperty("results").EnumerateArray());
            Assert.Equal(HttpStatusCode.OK, hybridStatus);
            Assert.Empty(hybridPayload.GetProperty("results").EnumerateArray());
            Assert.Equal(HttpStatusCode.OK, contextStatus);
            Assert.Empty(contextPayload.GetProperty("roleMemory").EnumerateArray());
            Assert.DoesNotContain(privateLensText, fullTextBody, StringComparison.Ordinal);
            Assert.DoesNotContain(privateLensText, semanticBody, StringComparison.Ordinal);
            Assert.DoesNotContain(privateLensText, hybridBody, StringComparison.Ordinal);
            Assert.DoesNotContain(privateLensText, contextBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Search_endpoints_exclude_session_scoped_chunks_until_session_access_is_modeled()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_search_session_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareSessionSearchFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (fullTextStatus, fullTextPayload, fullTextBody) = await SendSearchAsync(
                client,
                "session scoped launch code");
            var (semanticStatus, semanticPayload, semanticBody) = await SendSemanticSearchAsync(
                client,
                "session scoped launch code");
            var (hybridStatus, hybridPayload, hybridBody) = await SendHybridSearchAsync(
                client,
                "session scoped launch code",
                scopeType: "session",
                scopeId: "session-1");

            Assert.Equal(HttpStatusCode.OK, fullTextStatus);
            Assert.Equal(HttpStatusCode.OK, semanticStatus);
            Assert.Equal(HttpStatusCode.OK, hybridStatus);
            Assert.Empty(fullTextPayload.GetProperty("results").EnumerateArray());
            Assert.Empty(semanticPayload.GetProperty("results").EnumerateArray());
            Assert.Empty(hybridPayload.GetProperty("results").EnumerateArray());
            Assert.DoesNotContain("session scoped launch code", fullTextBody, StringComparison.Ordinal);
            Assert.DoesNotContain("session scoped launch code", semanticBody, StringComparison.Ordinal);
            Assert.DoesNotContain("session scoped launch code", hybridBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Search_endpoints_exclude_archived_project_chunks_for_org_admin_and_role_grant_paths()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_search_archived_project_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);
            await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
                databaseConnectionString,
                OrgAId,
                ProjectAId,
                projectStatus: "archived");
            await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(
                databaseConnectionString,
                OrgAId,
                PrincipalId,
                "admin");
            await ApiDatabaseTestSupport.InsertRoleAssignmentAsync(
                databaseConnectionString,
                PrincipalId,
                "cto",
                "project",
                ProjectAId);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{ProjectAId}/decisions",
                "read",
                principalId: PrincipalId);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{ProjectAId}/decisions",
                "read",
                roleId: "cto");
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                ProjectAEventId,
                PrincipalId,
                "project",
                ProjectAId.ToString(),
                scopeOrgId: OrgAId,
                scopeProjectId: ProjectAId,
                trustLevel: "human_approved");

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var repository = new PostgresMemoryFactRepository(dataSource);
            var archivedProjectMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
                new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
                $"/project/{ProjectAId}/decisions",
                "decision",
                "project_shared",
                "Archived project retrieval policy",
                "uses",
                "archived project retrieval policy that must stay hidden from search results",
                0.950m,
                ProjectAEventId,
                PrincipalId));
            await EmbedMemoryChunksAsync(dataSource);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (fullTextStatus, fullTextPayload, fullTextBody) = await SendSearchAsync(
                client,
                "archived project retrieval policy");
            var (semanticStatus, semanticPayload, semanticBody) = await SendSemanticSearchAsync(
                client,
                "archived project retrieval policy");
            var (hybridStatus, hybridPayload, hybridBody) = await SendHybridSearchAsync(
                client,
                "archived project retrieval policy",
                scopeType: "project",
                scopeId: ProjectAId.ToString());

            Assert.Equal(HttpStatusCode.OK, fullTextStatus);
            Assert.Equal(HttpStatusCode.OK, semanticStatus);
            Assert.Equal(HttpStatusCode.OK, hybridStatus);
            Assert.Empty(fullTextPayload.GetProperty("results").EnumerateArray());
            Assert.Empty(semanticPayload.GetProperty("results").EnumerateArray());
            Assert.Empty(hybridPayload.GetProperty("results").EnumerateArray());
            Assert.DoesNotContain(archivedProjectMemory.Id.ToString(), fullTextBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(archivedProjectMemory.Id.ToString(), semanticBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(archivedProjectMemory.Id.ToString(), hybridBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_hybrid_search_combines_ranking_components_inside_authorized_results()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_hybrid_search_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var (targetMemoryId, staleMemoryId, orgMemoryId, unauthorizedMemoryId, query) =
                await PrepareHybridSearchFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendHybridSearchAsync(
                client,
                query,
                scopeType: "project",
                scopeId: ProjectAId.ToString());

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var results = payload.GetProperty("results").EnumerateArray().ToArray();

            Assert.True(results.Length >= 2);
            Assert.Equal(targetMemoryId, results[0].GetProperty("sourceId").GetGuid());
            Assert.DoesNotContain(unauthorizedMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);

            var target = results.Single(result => result.GetProperty("sourceId").GetGuid() == targetMemoryId);
            var stale = results.Single(result => result.GetProperty("sourceId").GetGuid() == staleMemoryId);
            var org = results.Single(result => result.GetProperty("sourceId").GetGuid() == orgMemoryId);
            var targetComponents = target.GetProperty("components");
            var staleComponents = stale.GetProperty("components");
            var orgComponents = org.GetProperty("components");

            Assert.True(target.GetProperty("rank").GetDouble() > stale.GetProperty("rank").GetDouble());
            Assert.True(targetComponents.GetProperty("confidence").GetDouble() > staleComponents.GetProperty("confidence").GetDouble());
            Assert.True(targetComponents.GetProperty("recency").GetDouble() > staleComponents.GetProperty("recency").GetDouble());
            Assert.True(targetComponents.GetProperty("authority").GetDouble() > staleComponents.GetProperty("authority").GetDouble());
            Assert.Equal(1.0d, targetComponents.GetProperty("scopeMatch").GetDouble(), precision: 3);
            Assert.Equal("org", org.GetProperty("scopeType").GetString());
            Assert.Equal(OrgAId.ToString(), org.GetProperty("scopeId").GetString());
            Assert.Equal(0.80d, orgComponents.GetProperty("scopeMatch").GetDouble(), precision: 3);
            Assert.Equal(
                ComputeHybridScore(targetComponents),
                target.GetProperty("rank").GetDouble(),
                precision: 6);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_hybrid_search_rejects_partial_target_scope()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_hybrid_invalid_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendHybridSearchAsync(
                client,
                "hybrid ranking",
                scopeType: "project");

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory hybrid search is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("scopeType", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Search_endpoints_reject_noncanonical_target_scope_and_role_values()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_search_scope_validation_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (invalidProjectStatus, invalidProjectPayload, _) = await SendHybridSearchAsync(
                client,
                "hybrid ranking",
                scopeType: "project",
                scopeId: "not-a-guid");
            var (invalidGlobalStatus, invalidGlobalPayload, _) = await SendHybridSearchAsync(
                client,
                "hybrid ranking",
                scopeType: "global",
                scopeId: ProjectAId.ToString());
            var (invalidRoleStatus, invalidRolePayload, _) = await SendContextPacketAsync(
                client,
                "context packet",
                roleId: "intern");

            Assert.Equal(HttpStatusCode.BadRequest, invalidProjectStatus);
            Assert.Contains("valid GUID", invalidProjectPayload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.BadRequest, invalidGlobalStatus);
            Assert.Contains("'global'", invalidGlobalPayload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.BadRequest, invalidRoleStatus);
            Assert.Contains("roleId", invalidRolePayload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_context_returns_compact_source_identified_explainable_packet()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_context_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareContextPacketFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendContextPacketAsync(
                client,
                "cto context packet concise decision logs authorization predicates operational reversibility",
                scopeType: "project",
                scopeId: ProjectAId.ToString(),
                roleId: "cto",
                limit: 6);

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Equal(PrincipalId, payload.GetProperty("principalId").GetGuid());
            Assert.Equal("cto", payload.GetProperty("roleId").GetString());
            Assert.Equal("project", payload.GetProperty("targetScope").GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), payload.GetProperty("targetScope").GetProperty("scopeId").GetString());
            Assert.Equal("cto", payload.GetProperty("currentTask").GetProperty("roleId").GetString());

            var userPreference = Assert.Single(payload.GetProperty("userPreferences").EnumerateArray());
            Assert.Equal("user_preference", userPreference.GetProperty("kind").GetString());
            Assert.True(userPreference.GetProperty("content").GetString()!.Length <= 360);
            Assert.Equal(fixture.UserPreferenceEventId, userPreference.GetProperty("sourceEventId").GetGuid());
            Assert.Equal(JsonValueKind.Null, userPreference.GetProperty("sourceLink").ValueKind);
            Assert.True(userPreference.GetProperty("explanation").GetProperty("rank").GetDouble() > 0);
            Assert.True(userPreference.GetProperty("explanation").GetProperty("components").GetProperty("relevance").GetDouble() >= 0);
            Assert.Contains("confidence", userPreference.GetProperty("explanation").GetProperty("summary").GetString(), StringComparison.Ordinal);

            var relevantDecision = Assert.Single(payload.GetProperty("relevantDecisions").EnumerateArray());
            Assert.Equal(fixture.ProjectDecisionId, relevantDecision.GetProperty("sourceId").GetGuid());
            Assert.Equal("project_decision", relevantDecision.GetProperty("kind").GetString());
            Assert.Equal(JsonValueKind.Null, relevantDecision.GetProperty("sourceLink").ValueKind);

            var roleMemory = Assert.Single(payload.GetProperty("roleMemory").EnumerateArray());
            Assert.Equal("project_role_lens", roleMemory.GetProperty("kind").GetString());
            Assert.Equal(fixture.RoleMemoryLensId, roleMemory.GetProperty("sourceId").GetGuid());
            Assert.Equal(fixture.ProjectDecisionId, roleMemory.GetProperty("baseMemoryFactId").GetGuid());
            Assert.Equal(JsonValueKind.Null, roleMemory.GetProperty("sourceLink").ValueKind);

            var sourceEvents = payload
                .GetProperty("sourceEvents")
                .EnumerateArray()
                .ToArray();
            var sourceEventIds = sourceEvents
                .Select(sourceEvent => sourceEvent.GetProperty("id").GetGuid())
                .ToArray();

            Assert.Contains(fixture.UserPreferenceEventId, sourceEventIds);
            Assert.Contains(fixture.ProjectDecisionEventId, sourceEventIds);
            Assert.Contains(fixture.RoleLensEventId, sourceEventIds);
            Assert.All(sourceEvents, sourceEvent => Assert.Equal(JsonValueKind.Null, sourceEvent.GetProperty("link").ValueKind));
            Assert.DoesNotContain(fixture.CfoRoleLensEventId, sourceEventIds);
            Assert.DoesNotContain(fixture.ProjectBDecisionEventId, sourceEventIds);
            Assert.DoesNotContain(fixture.CfoRoleMemoryLensId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(fixture.ProjectBDecisionId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Project B private decision", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_context_filters_role_specific_candidates_before_ranking_limit()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_context_role_limit_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var ctoMemoryId = await PrepareRoleFilteredContextOverflowFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendContextPacketAsync(
                client,
                "role filtered ranking saturation signal",
                scopeType: "project",
                scopeId: ProjectAId.ToString(),
                roleId: "cto",
                limit: 1);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var roleMemory = Assert.Single(payload.GetProperty("roleMemory").EnumerateArray());

            Assert.Equal(ctoMemoryId, roleMemory.GetProperty("sourceId").GetGuid());
            Assert.Equal($"/project/{ProjectAId}/role/cto/lens", roleMemory.GetProperty("namespace").GetString());
            Assert.DoesNotContain($"/project/{ProjectAId}/role/cfo/lens", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_context_can_be_scored_with_retrieval_evaluation_metrics()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_context_eval_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareContextPacketFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendContextPacketAsync(
                client,
                "cto context packet concise decision logs authorization predicates operational reversibility",
                scopeType: "project",
                scopeId: ProjectAId.ToString(),
                roleId: "cto",
                limit: 6);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var sessionOnlyCandidateId = Guid.Parse("88888888-8888-4888-8888-888888888888");
            var evaluation = MemoryRetrievalEvaluator.Evaluate(
                new MemoryRetrievalEvaluationCase(
                    RelevantSourceIds:
                    [
                        fixture.UserPreferenceId,
                        fixture.ProjectDecisionId,
                        fixture.RoleMemoryLensId
                    ],
                    AllowedSourceIds:
                    [
                        fixture.UserPreferenceId,
                        fixture.ProjectDecisionId,
                        fixture.RoleMemoryLensId
                    ],
                    ForbiddenSourceIds: [fixture.ProjectBDecisionId],
                    ContradictedSourceIds: [fixture.ContradictedProjectDecisionId],
                    MaxItemCount: 6,
                    MaxContentLength: 360,
                    WriteObservations:
                    [
                        new MemoryRetrievalWriteObservation(
                            fixture.UserPreferenceId,
                            ExpectedDurable: true,
                            StoredDurably: true),
                        new MemoryRetrievalWriteObservation(
                            sessionOnlyCandidateId,
                            ExpectedDurable: false,
                            StoredDurably: false),
                        new MemoryRetrievalWriteObservation(
                            fixture.ContradictedProjectDecisionId,
                            ExpectedDurable: false,
                            StoredDurably: false,
                            ExpectedContradiction: true,
                            RoutedAsContradiction: true)
                    ]),
                ReadContextPacketEvaluationItems(payload));

            Assert.Equal(1.0d, evaluation.Relevance, precision: 3);
            Assert.True(evaluation.IsCompact);
            Assert.Equal(1.0d, evaluation.Compactness, precision: 3);
            Assert.Equal(1.0d, evaluation.WritePrecision, precision: 3);
            Assert.Equal(0, evaluation.RetrievalFalsePositiveCount);
            Assert.Equal(0, evaluation.DurableWriteFalsePositiveCount);
            Assert.Equal(0.0d, evaluation.FalsePositiveRate, precision: 3);
            Assert.Equal(1.0d, evaluation.ContradictionQuality, precision: 3);
            Assert.Empty(evaluation.MissingRelevantSourceIds);
            Assert.Empty(evaluation.FalsePositiveSourceIds);
            Assert.Empty(evaluation.FalsePositiveWriteCandidateIds);
            Assert.Empty(evaluation.OversizedSourceIds);
            Assert.Empty(evaluation.RetrievedContradictedSourceIds);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_context_rejects_large_packet_limit()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_context_limit_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendContextPacketAsync(
                client,
                "context packet",
                limit: 13);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory context request is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("between 1 and 12", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task<(Guid ProjectAMemoryId, Guid ProjectBMemoryId)> PrepareSearchFixtureAsync(
        string connectionString)
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
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectAEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectBEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new PostgresMemoryFactRepository(dataSource);
        var projectAMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "Project A retrieval decision",
            "uses",
            "postgres audit retrieval",
            0.950m,
            ProjectAEventId,
            PrincipalId));
        var projectBMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectBId.ToString(), OrgId: OrgBId, ProjectId: ProjectBId),
            $"/project/{ProjectBId}/decisions",
            "decision",
            "project_shared",
            "Project B retrieval decision",
            "uses",
            "postgres audit retrieval",
            0.950m,
            ProjectBEventId,
            PrincipalId));

        return (projectAMemory.Id, projectBMemory.Id);
    }

    private static async Task<(Guid ProjectATargetMemoryId, Guid ProjectBMemoryId, string Query)>
        PrepareSemanticSearchFixtureAsync(string connectionString)
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
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectAEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectBEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId);

        const string targetSubject = "semantic retrieval target";
        const string targetObject = "pgvector cosine recall";
        var exactQuery = $"{targetSubject} {targetSubject} uses {targetObject}";

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new PostgresMemoryFactRepository(dataSource);
        var projectATargetMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            targetSubject,
            "uses",
            targetObject,
            0.950m,
            ProjectAEventId,
            PrincipalId));
        var projectBMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectBId.ToString(), OrgId: OrgBId, ProjectId: ProjectBId),
            $"/project/{ProjectBId}/decisions",
            "decision",
            "project_shared",
            targetSubject,
            "uses",
            targetObject,
            0.950m,
            ProjectBEventId,
            PrincipalId));
        await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "semantic distractor",
            "uses",
            "calendar planning",
            0.950m,
            ProjectAEventId,
            PrincipalId));

        await EmbedMemoryChunksAsync(dataSource);

        return (projectATargetMemory.Id, projectBMemory.Id, exactQuery);
    }

    private static async Task PrepareSessionSearchFixtureAsync(string connectionString)
    {
        const string sessionId = "session-1";
        var sessionEventId = Guid.NewGuid();

        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/session/{sessionId}/instructions",
            "read",
            principalId: PrincipalId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            sessionEventId,
            PrincipalId,
            "session",
            sessionId);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new PostgresMemoryFactRepository(dataSource);

        await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("session", sessionId),
            $"/session/{sessionId}/instructions",
            "session_instruction",
            "private",
            "session scoped launch code",
            "uses",
            "session scoped launch code",
            0.950m,
            sessionEventId,
            PrincipalId));

        await EmbedMemoryChunksAsync(dataSource);
    }

    private static async Task<(Guid TargetMemoryId, Guid StaleMemoryId, Guid OrgMemoryId, Guid UnauthorizedMemoryId, string Query)>
        PrepareHybridSearchFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgAId, ProjectAId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgBId, ProjectBId);
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(
            connectionString,
            OrgAId,
            PrincipalId,
            "reader");
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
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/org/{OrgAId}/principles",
            "read",
            principalId: PrincipalId);

        var trustedProjectAEventId = Guid.NewGuid();
        var staleProjectAEventId = Guid.NewGuid();
        var orgAEventId = Guid.NewGuid();
        var projectBEventId = Guid.NewGuid();

        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            trustedProjectAEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "human_approved");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            staleProjectAEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "web_content");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            orgAEventId,
            PrincipalId,
            "org",
            OrgAId.ToString(),
            scopeOrgId: OrgAId,
            trustLevel: "human_approved");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            projectBEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId,
            trustLevel: "human_approved");

        const string query = "hybrid ranking safety formula";

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new PostgresMemoryFactRepository(dataSource);
        var targetMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "hybrid ranking preferred memory",
            "uses",
            "hybrid ranking safety formula",
            0.950m,
            trustedProjectAEventId,
            PrincipalId));
        var staleMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "hybrid ranking stale memory",
            "uses",
            "hybrid ranking safety formula",
            0.400m,
            staleProjectAEventId,
            PrincipalId));
        var orgMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("org", OrgAId.ToString(), OrgId: OrgAId),
            $"/org/{OrgAId}/principles",
            "principle",
            "org_shared",
            "hybrid ranking org memory",
            "uses",
            "hybrid ranking safety formula",
            0.700m,
            orgAEventId,
            PrincipalId));
        var unauthorizedMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectBId.ToString(), OrgId: OrgBId, ProjectId: ProjectBId),
            $"/project/{ProjectBId}/decisions",
            "decision",
            "project_shared",
            "hybrid ranking unauthorized memory",
            "uses",
            "hybrid ranking safety formula",
            0.990m,
            projectBEventId,
            PrincipalId));

        await SetMemoryCreatedAtAsync(connectionString, staleMemory.Id, DateTimeOffset.UtcNow.AddDays(-120));
        await EmbedMemoryChunksAsync(dataSource);

        return (targetMemory.Id, staleMemory.Id, orgMemory.Id, unauthorizedMemory.Id, query);
    }

    private static async Task PrepareRoleSpecificSearchFixtureWithoutAssignmentAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgAId, ProjectAId);
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
            connectionString,
            ProjectAId,
            PrincipalId,
            "reader");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/role/cto/lens",
            "read",
            principalId: PrincipalId);

        var sourceEventId = Guid.NewGuid();
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            sourceEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "human_approved");

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var memoryFacts = new PostgresMemoryFactRepository(dataSource);
        var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);
        var baseMemory = await memoryFacts.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "Role boundary base fact",
            "uses",
            "ordinary project context",
            0.900m,
            sourceEventId,
            PrincipalId));

        await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            "cto",
            baseMemory.Id,
            "role boundary sentinel cto lens must stay behind a role assignment",
            0.920m,
            sourceEventId,
            PrincipalId));

        await EmbedMemoryChunksAsync(dataSource);
    }

    private static async Task<ContextPacketFixture> PrepareContextPacketFixtureAsync(string connectionString)
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
        await ApiDatabaseTestSupport.InsertRoleAssignmentAsync(
            connectionString,
            PrincipalId,
            "cto",
            "project",
            ProjectAId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/user/{PrincipalId}/preferences",
            "read",
            principalId: PrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/decisions",
            "read",
            principalId: PrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/role/cto/lens",
            "read",
            principalId: PrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/role/cfo/lens",
            "read",
            principalId: PrincipalId);

        var userPreferenceEventId = Guid.NewGuid();
        var projectDecisionEventId = Guid.NewGuid();
        var contradictedProjectDecisionEventId = Guid.NewGuid();
        var roleLensEventId = Guid.NewGuid();
        var cfoRoleLensEventId = Guid.NewGuid();
        var projectBDecisionEventId = Guid.NewGuid();

        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            userPreferenceEventId,
            PrincipalId,
            "user",
            PrincipalId.ToString());
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            projectDecisionEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "human_approved");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            contradictedProjectDecisionEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "human_approved");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            roleLensEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "human_approved");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            cfoRoleLensEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "human_approved");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            projectBDecisionEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId,
            trustLevel: "human_approved");

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var memoryFacts = new PostgresMemoryFactRepository(dataSource);
        var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);
        var userPreference = await memoryFacts.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("user", PrincipalId.ToString(), PrincipalId: PrincipalId),
            $"/user/{PrincipalId}/preferences",
            "preference",
            "private",
            "technical planning format",
            "prefers",
            "concise decision logs with short rationale and explicit tradeoffs for technical planning " +
            "while keeping packet text compact enough for prompt assembly and avoiding unnecessary repetition " +
            "across the final context packet response",
            0.900m,
            userPreferenceEventId,
            PrincipalId));
        var projectDecision = await memoryFacts.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "Long-Term Memory System retrieval",
            "uses",
            "authorization predicates before hybrid ranking and context packet construction",
            0.950m,
            projectDecisionEventId,
            PrincipalId));
        var contradictedProjectDecision = await memoryFacts.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "Long-Term Memory System retrieval",
            "uses",
            "client-side filtering after ranking",
            0.910m,
            contradictedProjectDecisionEventId,
            PrincipalId,
            MemoryFactStatuses.Contradicted));
        var projectBDecision = await memoryFacts.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectBId.ToString(), OrgId: OrgBId, ProjectId: ProjectBId),
            $"/project/{ProjectBId}/decisions",
            "decision",
            "project_shared",
            "Project B private decision",
            "uses",
            "confidential retrieval policy that must not leak into Project A packets",
            0.990m,
            projectBDecisionEventId,
            PrincipalId));
        var roleLens = await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            "cto",
            projectDecision.Id,
            "CTO context packet should foreground operational reversibility, authorization boundaries, delivery sequencing, and source-linked explanations.",
            0.920m,
            roleLensEventId,
            PrincipalId));
        var cfoRoleLens = await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            "cfo",
            projectDecision.Id,
            "CFO context packet should foreground budget reversibility, authorization boundaries, delivery sequencing, and source-linked explanations.",
            0.930m,
            cfoRoleLensEventId,
            PrincipalId));

        await EmbedMemoryChunksAsync(dataSource);

        return new ContextPacketFixture(
            userPreference.Id,
            projectDecision.Id,
            roleLens.Id,
            cfoRoleLens.Id,
            projectBDecision.Id,
            contradictedProjectDecision.Id,
            userPreferenceEventId,
            projectDecisionEventId,
            roleLensEventId,
            cfoRoleLensEventId,
            projectBDecisionEventId,
            contradictedProjectDecisionEventId);
    }

    private static async Task<Guid> PrepareRoleFilteredContextOverflowFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgAId, ProjectAId);
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
            connectionString,
            ProjectAId,
            PrincipalId,
            "reader");
        await ApiDatabaseTestSupport.InsertRoleAssignmentAsync(
            connectionString,
            PrincipalId,
            "cto",
            "project",
            ProjectAId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/role/cto/lens",
            "read",
            principalId: PrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/role/cfo/lens",
            "read",
            principalId: PrincipalId);

        var sourceEventId = Guid.NewGuid();
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            sourceEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId,
            trustLevel: "human_approved");

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var memoryFacts = new PostgresMemoryFactRepository(dataSource);

        for (var index = 0; index < 55; index++)
        {
            await memoryFacts.StoreAsync(new MemoryFactWriteCommand(
                new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
                $"/project/{ProjectAId}/role/cfo/lens/overflow-{index}",
                "principle",
                "project_shared",
                $"CFO role filtered ranking saturation signal {index}",
                "uses",
                "role filtered ranking saturation signal with finance priority context",
                1.000m,
                sourceEventId,
                PrincipalId));
        }

        var ctoMemory = await memoryFacts.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/role/cto/lens",
            "principle",
            "project_shared",
            "CTO role filtered ranking saturation signal",
            "uses",
            "role filtered ranking saturation signal with technology priority context",
            0.500m,
            sourceEventId,
            PrincipalId));

        await EmbedMemoryChunksAsync(dataSource);

        return ctoMemory.Id;
    }

    private static async Task SetMemoryCreatedAtAsync(
        string connectionString,
        Guid memoryFactId,
        DateTimeOffset createdAt)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE memory_facts
            SET created_at = @created_at
            WHERE id = @memory_fact_id;

            UPDATE memory_chunks
            SET created_at = @created_at
            WHERE source_type = 'memory_fact'
                AND source_id = @memory_fact_id;
            """,
            connection);
        command.Parameters.AddWithValue("created_at", createdAt);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task EmbedMemoryChunksAsync(NpgsqlDataSource dataSource)
    {
        var chunks = new List<(Guid ChunkId, string Input)>();

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(
            """
            SELECT id, concat_ws(' ', title, content) AS input
            FROM memory_chunks
            ORDER BY created_at, id;
            """,
            connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                chunks.Add((reader.GetGuid(0), reader.GetString(1)));
            }
        }

        var provider = new DeterministicMemoryEmbeddingProvider(Options.Create(new MemoryEmbeddingOptions()));
        var store = new PostgresMemoryChunkEmbeddingStore(dataSource);

        foreach (var chunk in chunks)
        {
            var embedding = await provider.EmbedAsync(new MemoryEmbeddingRequest(chunk.Input));
            await store.StoreAsync(new MemoryChunkEmbeddingWriteCommand(
                chunk.ChunkId,
                embedding.Model,
                embedding.Dimension,
                embedding.Values));
        }
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendSearchAsync(
        HttpClient client,
        string query,
        int limit = 10)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/memory/search?q={Uri.EscapeDataString(query)}&limit={limit}");
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendSemanticSearchAsync(
        HttpClient client,
        string query,
        int limit = 10)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/memory/search/semantic?q={Uri.EscapeDataString(query)}&limit={limit}");
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendHybridSearchAsync(
        HttpClient client,
        string query,
        int limit = 10,
        string? scopeType = null,
        string? scopeId = null)
    {
        var uri = $"/api/memory/search/hybrid?q={Uri.EscapeDataString(query)}&limit={limit}";

        if (!string.IsNullOrWhiteSpace(scopeType))
        {
            uri += $"&scopeType={Uri.EscapeDataString(scopeType)}";
        }

        if (!string.IsNullOrWhiteSpace(scopeId))
        {
            uri += $"&scopeId={Uri.EscapeDataString(scopeId)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendContextPacketAsync(
        HttpClient client,
        string query,
        int limit = 10,
        string? scopeType = null,
        string? scopeId = null,
        string? roleId = null)
    {
        var uri = $"/api/memory/context?q={Uri.EscapeDataString(query)}&limit={limit}";

        if (!string.IsNullOrWhiteSpace(scopeType))
        {
            uri += $"&scopeType={Uri.EscapeDataString(scopeType)}";
        }

        if (!string.IsNullOrWhiteSpace(scopeId))
        {
            uri += $"&scopeId={Uri.EscapeDataString(scopeId)}";
        }

        if (!string.IsNullOrWhiteSpace(roleId))
        {
            uri += $"&roleId={Uri.EscapeDataString(roleId)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static double ComputeHybridScore(JsonElement components)
    {
        return (components.GetProperty("relevance").GetDouble() * 0.40d)
            + (components.GetProperty("confidence").GetDouble() * 0.25d)
            + (components.GetProperty("recency").GetDouble() * 0.15d)
            + (components.GetProperty("authority").GetDouble() * 0.15d)
            + (components.GetProperty("scopeMatch").GetDouble() * 0.05d);
    }

    private static IReadOnlyList<MemoryRetrievalEvaluationItem> ReadContextPacketEvaluationItems(JsonElement payload)
    {
        var items = new List<MemoryRetrievalEvaluationItem>();

        foreach (var groupName in new[] { "userPreferences", "projectMemory", "roleMemory", "relevantDecisions" })
        {
            foreach (var item in payload.GetProperty(groupName).EnumerateArray())
            {
                items.Add(new MemoryRetrievalEvaluationItem(
                    item.GetProperty("sourceId").GetGuid(),
                    item.GetProperty("content").GetString() ?? string.Empty));
            }
        }

        return items;
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, PrincipalId.ToString());
    }

    private sealed record ContextPacketFixture(
        Guid UserPreferenceId,
        Guid ProjectDecisionId,
        Guid RoleMemoryLensId,
        Guid CfoRoleMemoryLensId,
        Guid ProjectBDecisionId,
        Guid ContradictedProjectDecisionId,
        Guid UserPreferenceEventId,
        Guid ProjectDecisionEventId,
        Guid RoleLensEventId,
        Guid CfoRoleLensEventId,
        Guid ProjectBDecisionEventId,
        Guid ContradictedProjectDecisionEventId);
}
