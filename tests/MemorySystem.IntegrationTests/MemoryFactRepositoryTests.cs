using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryFacts;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class MemoryFactRepositoryTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid AgentPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OrgId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid UserEventId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid ProjectEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid RoleEventId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid AgentEventId = Guid.Parse("88888888-8888-4888-8888-888888888888");

    [Fact]
    [Trait("Category", "Database")]
    public async Task StoreAsync_round_trips_user_project_role_and_agent_private_memory()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_fact_repository_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var repository = new PostgresMemoryFactRepository(dataSource);

            var userPreference = await repository.StoreAsync(CreateWriteCommand(
                UserScope(),
                $"/user/{PrincipalId}/preferences",
                "preference",
                "private",
                "technical planning format",
                "concise decision logs",
                UserEventId,
                PrincipalId));
            var projectDecision = await repository.StoreAsync(CreateWriteCommand(
                ProjectScope(),
                $"/project/{ProjectId}/decisions",
                "decision",
                "project_shared",
                "storage engine",
                "postgres plus pgvector",
                ProjectEventId,
                PrincipalId));
            var roleMemory = await repository.StoreAsync(CreateWriteCommand(
                RoleScope(),
                "/role/cto/shared",
                "role_principle",
                "role_shared",
                "cto review",
                "prioritizes auditability",
                RoleEventId,
                PrincipalId));
            var agentPrivate = await repository.StoreAsync(CreateWriteCommand(
                AgentScope(),
                $"/agent/{AgentPrincipalId}/private",
                "agent_private",
                "private",
                "indexing worker",
                "prefers short leases",
                AgentEventId,
                AgentPrincipalId));

            Assert.Equal(PrincipalId, userPreference.UserPrincipalId);
            Assert.Equal("tool_output", userPreference.TrustLevel);
            Assert.Equal(ProjectId, projectDecision.ProjectId);
            Assert.Equal(OrgId, projectDecision.OrgId);
            Assert.Equal("cto", roleMemory.RoleId);
            Assert.Equal(AgentPrincipalId, agentPrivate.AgentPrincipalId);

            await AssertSingleScopedFactAsync(repository, UserScope(), "preference", userPreference.Id);
            await AssertSingleScopedFactAsync(repository, ProjectScope(), "decision", projectDecision.Id);
            await AssertSingleScopedFactAsync(repository, RoleScope(), "role_principle", roleMemory.Id);
            await AssertSingleScopedFactAsync(repository, AgentScope(), "agent_private", agentPrivate.Id);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task FindByScopeAsync_filters_lifecycle_statuses_for_normal_retrieval()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_fact_status_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var repository = new PostgresMemoryFactRepository(dataSource);
            var expectedByStatus = new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (var status in MemoryFactStatuses.All)
            {
                var memoryFact = await repository.StoreAsync(CreateWriteCommand(
                    UserScope(),
                    $"/user/{PrincipalId}/preferences",
                    "preference",
                    "private",
                    $"technical planning format {status}",
                    "concise decision logs",
                    UserEventId,
                    PrincipalId) with
                {
                    Status = status
                });

                expectedByStatus.Add(status, memoryFact.Id);
            }

            var normalResults = await repository.FindByScopeAsync(
                new MemoryFactScopeQuery(UserScope(), "preference"));
            var normalResult = Assert.Single(normalResults);

            Assert.Equal(expectedByStatus[MemoryFactStatuses.Active], normalResult.Id);
            Assert.Equal(MemoryFactStatuses.Active, normalResult.Status);

            foreach (var status in MemoryFactStatuses.All)
            {
                var statusResults = await repository.FindByScopeAsync(
                    new MemoryFactScopeQuery(UserScope(), "preference", status));
                var statusResult = Assert.Single(statusResults);

                Assert.Equal(expectedByStatus[status], statusResult.Id);
                Assert.Equal(status, statusResult.Status);
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task SearchAsync_filters_by_scope_type_subject_and_status_without_vector_retrieval()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_fact_search_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var repository = new PostgresMemoryFactRepository(dataSource);

            var expected = await repository.StoreAsync(CreateWriteCommand(
                UserScope(),
                $"/user/{PrincipalId}/preferences",
                "preference",
                "private",
                "editor theme",
                "dark mode",
                UserEventId,
                PrincipalId));
            await repository.StoreAsync(CreateWriteCommand(
                UserScope(),
                $"/user/{PrincipalId}/preferences",
                "preference",
                "private",
                "planning format",
                "concise decision logs",
                UserEventId,
                PrincipalId));
            await repository.StoreAsync(CreateWriteCommand(
                UserScope(),
                $"/user/{PrincipalId}/decisions",
                "decision",
                "private",
                "editor theme decision",
                "use a neutral theme",
                UserEventId,
                PrincipalId));
            await repository.StoreAsync(CreateWriteCommand(
                ProjectScope(),
                $"/project/{ProjectId}/preferences",
                "preference",
                "project_shared",
                "editor theme",
                "solarized",
                ProjectEventId,
                PrincipalId));
            var expired = await repository.StoreAsync(CreateWriteCommand(
                UserScope(),
                $"/user/{PrincipalId}/preferences",
                "preference",
                "private",
                "editor theme archive",
                "light mode",
                UserEventId,
                PrincipalId) with
            {
                Status = MemoryFactStatuses.Expired
            });

            var activeResults = await repository.SearchAsync(new MemoryFactSearchQuery(
                UserScope(),
                "preference",
                "THEME",
                MemoryFactStatuses.Active));
            var activeResult = Assert.Single(activeResults);

            Assert.Equal(expected.Id, activeResult.Id);
            Assert.Equal("preference", activeResult.MemoryType);
            Assert.Equal(MemoryFactStatuses.Active, activeResult.Status);
            Assert.Contains("theme", activeResult.Subject, StringComparison.OrdinalIgnoreCase);

            var expiredResults = await repository.SearchAsync(new MemoryFactSearchQuery(
                UserScope(),
                "preference",
                "theme",
                MemoryFactStatuses.Expired));
            var expiredResult = Assert.Single(expiredResults);

            Assert.Equal(expired.Id, expiredResult.Id);
            Assert.Equal(MemoryFactStatuses.Expired, expiredResult.Status);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task PrepareDatabaseAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(
            connectionString,
            AgentPrincipalId,
            principalType: "agent",
            displayName: "Indexing Agent");
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgId, ProjectId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            UserEventId,
            PrincipalId,
            "user",
            PrincipalId.ToString(),
            trustLevel: "tool_output");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectEventId,
            PrincipalId,
            "project",
            ProjectId.ToString(),
            scopeOrgId: OrgId,
            scopeProjectId: ProjectId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            RoleEventId,
            PrincipalId,
            "role",
            "cto",
            scopeRoleId: "cto");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            AgentEventId,
            AgentPrincipalId,
            "agent",
            AgentPrincipalId.ToString());
    }

    private static MemoryFactWriteCommand CreateWriteCommand(
        MemoryScopeResolution scope,
        string namespaceValue,
        string memoryType,
        string visibility,
        string subject,
        string objectValue,
        Guid sourceEventId,
        Guid proposedByPrincipalId)
    {
        return new MemoryFactWriteCommand(
            scope,
            namespaceValue,
            memoryType,
            visibility,
            subject,
            "stores",
            objectValue,
            0.950m,
            sourceEventId,
            proposedByPrincipalId);
    }

    private static async Task AssertSingleScopedFactAsync(
        IMemoryFactRepository repository,
        MemoryScopeResolution scope,
        string memoryType,
        Guid expectedMemoryFactId)
    {
        var memoryFacts = await repository.FindByScopeAsync(new MemoryFactScopeQuery(scope, memoryType));
        var memoryFact = Assert.Single(memoryFacts);

        Assert.Equal(expectedMemoryFactId, memoryFact.Id);
        Assert.Equal(scope.ScopeType, memoryFact.ScopeType);
        Assert.Equal(scope.ScopeId, memoryFact.ScopeId);
        Assert.Equal(memoryType, memoryFact.MemoryType);
    }

    private static MemoryScopeResolution UserScope()
    {
        return new MemoryScopeResolution("user", PrincipalId.ToString(), PrincipalId: PrincipalId);
    }

    private static MemoryScopeResolution ProjectScope()
    {
        return new MemoryScopeResolution("project", ProjectId.ToString(), OrgId: OrgId, ProjectId: ProjectId);
    }

    private static MemoryScopeResolution RoleScope()
    {
        return new MemoryScopeResolution("role", "cto", RoleId: "cto", ScopeRoleId: "cto");
    }

    private static MemoryScopeResolution AgentScope()
    {
        return new MemoryScopeResolution(
            "agent",
            AgentPrincipalId.ToString(),
            PrincipalId: AgentPrincipalId,
            AgentPrincipalId: AgentPrincipalId);
    }
}
