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

public sealed partial class ApiMemorySearchTests
{
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
}
