using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.RoleMemoryLenses;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryFacts;
using MemorySystem.Infrastructure.RoleMemoryLenses;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class RoleMemoryLensRepositoryTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid AgentPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OrgId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid OtherProjectId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly Guid GlobalEventId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid OrgEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid ProjectEventId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid OtherProjectEventId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid LegacyAgentGlobalEventId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task StoreAsync_round_trips_shared_role_principles_and_project_role_lens_separately()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_repository_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var globalBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                GlobalScope(),
                "/global/role-principles",
                "role_source",
                "role_shared",
                "architecture decision reviews",
                "must preserve auditability",
                GlobalEventId));
            var orgBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                OrgScope(),
                $"/org/{OrgId}/principles",
                "role_source",
                "org_shared",
                "organization architecture reviews",
                "prioritize operational clarity",
                OrgEventId));
            var projectBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                ProjectScope(),
                $"/project/{ProjectId}/decisions",
                "decision",
                "project_shared",
                "storage engine",
                "postgres plus pgvector",
                ProjectEventId));

            var globalSharedLens = await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                GlobalScope(),
                "cto",
                globalBaseFact.Id,
                "For the CTO role, architecture decisions should make audit paths easy to inspect.",
                0.920m,
                GlobalEventId,
                PrincipalId));
            var orgSharedLens = await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                OrgScope(),
                "cto",
                orgBaseFact.Id,
                "For the CTO role in this organization, operational clarity is part of technical quality.",
                0.910m,
                OrgEventId,
                PrincipalId));
            var projectRoleLens = await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                ProjectScope(),
                "cto",
                projectBaseFact.Id,
                "For this project, SQL-first storage reduces authorization and audit risk.",
                0.900m,
                ProjectEventId,
                PrincipalId));

            Assert.True(globalSharedLens.IsSharedRolePrinciple);
            Assert.False(globalSharedLens.IsProjectRoleLens);
            Assert.Equal("global", globalSharedLens.ScopeType);
            Assert.Equal("global", globalSharedLens.ScopeId);
            Assert.Null(globalSharedLens.ProjectId);

            Assert.True(orgSharedLens.IsSharedRolePrinciple);
            Assert.Equal("org", orgSharedLens.ScopeType);
            Assert.Equal(OrgId.ToString(), orgSharedLens.ScopeId);
            Assert.Equal(OrgId, orgSharedLens.OrgId);
            Assert.Null(orgSharedLens.ProjectId);

            Assert.True(projectRoleLens.IsProjectRoleLens);
            Assert.Equal("project", projectRoleLens.ScopeType);
            Assert.Equal(ProjectId.ToString(), projectRoleLens.ScopeId);
            Assert.Equal(OrgId, projectRoleLens.OrgId);
            Assert.Equal(ProjectId, projectRoleLens.ProjectId);

            await AssertSingleScopedLensAsync(roleLenses, GlobalScope(), globalSharedLens.Id);
            await AssertSingleScopedLensAsync(roleLenses, OrgScope(), orgSharedLens.Id);
            await AssertSingleScopedLensAsync(roleLenses, ProjectScope(), projectRoleLens.Id);

            var cfoProjectLenses = await roleLenses.FindByScopeAsync(
                new RoleMemoryLensScopeQuery(ProjectScope(), "cfo"));

            Assert.Empty(cfoProjectLenses);
            Assert.Equal(6, await ApiDatabaseTestSupport.CountRowsAsync(databaseConnectionString, "memory_chunks"));
            Assert.Equal(6, await ApiDatabaseTestSupport.CountRowsAsync(databaseConnectionString, "outbox_jobs"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task StoreAsync_allows_project_role_lens_backed_by_target_organization_fact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_org_base_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var orgBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                OrgScope(),
                $"/org/{OrgId}/principles",
                "role_source",
                "org_shared",
                "organization operating principle",
                "prioritize operational clarity",
                OrgEventId));

            var projectRoleLens = await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                ProjectScope(),
                "cto",
                orgBaseFact.Id,
                "For this project, the organization operating principle applies to platform decisions.",
                0.900m,
                ProjectEventId,
                PrincipalId));

            Assert.True(projectRoleLens.IsProjectRoleLens);
            Assert.Equal(orgBaseFact.Id, projectRoleLens.BaseMemoryFactId);
            Assert.Equal(ProjectId, projectRoleLens.ProjectId);
            Assert.Equal(OrgId, projectRoleLens.OrgId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task StoreAsync_accepts_legacy_agent_source_event_actor()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_legacy_agent_source_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(
                databaseConnectionString,
                AgentPrincipalId,
                principalType: "agent",
                displayName: "Legacy Lens Agent");
            await ApiDatabaseTestSupport.InsertLegacyAgentSourceEventAsync(
                databaseConnectionString,
                LegacyAgentGlobalEventId,
                AgentPrincipalId,
                "global",
                "global");

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var globalBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                GlobalScope(),
                "/global/role-principles",
                "role_source",
                "role_shared",
                "legacy agent role lens source",
                "can propose shared interpretations",
                GlobalEventId));

            var lens = await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                GlobalScope(),
                "cto",
                globalBaseFact.Id,
                "A legacy agent source event can back a reviewed shared CTO interpretation.",
                0.900m,
                LegacyAgentGlobalEventId,
                AgentPrincipalId));

            Assert.Equal(LegacyAgentGlobalEventId, lens.SourceEventId);
            Assert.Equal(AgentPrincipalId, lens.ProposedByPrincipalId);
            Assert.True(lens.IsSharedRolePrinciple);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task StoreAsync_does_not_enqueue_indexing_for_inactive_role_lenses()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_inactive_indexing_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var projectBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                ProjectScope(),
                $"/project/{ProjectId}/decisions",
                "decision",
                "project_shared",
                "inactive role lens base",
                "can be referenced by an inactive lens",
                ProjectEventId));

            var deletedLens = await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                ProjectScope(),
                "cto",
                projectBaseFact.Id,
                "A deleted CTO lens should not be indexed.",
                0.900m,
                ProjectEventId,
                PrincipalId,
                Status: MemoryFactStatuses.Deleted));

            Assert.Equal(MemoryFactStatuses.Deleted, deletedLens.Status);
            Assert.Equal(1, await ApiDatabaseTestSupport.CountRowsAsync(databaseConnectionString, "memory_chunks"));
            Assert.Equal(1, await ApiDatabaseTestSupport.CountRowsAsync(databaseConnectionString, "outbox_jobs"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task StoreAsync_trims_interpretation_for_lens_and_index_chunk()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_trim_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var projectBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                ProjectScope(),
                $"/project/{ProjectId}/decisions",
                "decision",
                "project_shared",
                "trimmed role lens base",
                "backs a normalized role lens interpretation",
                ProjectEventId));

            var lens = await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                ProjectScope(),
                "cto",
                projectBaseFact.Id,
                "  Interpret architecture decisions with auditability in mind.  ",
                0.900m,
                ProjectEventId,
                PrincipalId));

            var indexedContent = await ReadMemoryChunkContentAsync(databaseConnectionString, lens.Id);

            Assert.Equal("Interpret architecture decisions with auditability in mind.", lens.Interpretation);
            Assert.Equal(lens.Interpretation, indexedContent);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task StoreAsync_rejects_active_role_lens_backed_by_inactive_fact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_inactive_base_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var deletedBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                ProjectScope(),
                $"/project/{ProjectId}/decisions",
                "decision",
                "project_shared",
                "deleted decision",
                "must not back active role lenses",
                ProjectEventId) with
            {
                Status = MemoryFactStatuses.Deleted
            });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                    ProjectScope(),
                    "cto",
                    deletedBaseFact.Id,
                    "An active CTO lens cannot be backed by deleted durable memory.",
                    0.900m,
                    ProjectEventId,
                    PrincipalId)));

            Assert.Contains("active memory facts", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Database_rejects_promoting_inactive_role_lens_backed_by_inactive_fact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_promote_inactive_base_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var deletedBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                ProjectScope(),
                $"/project/{ProjectId}/decisions",
                "decision",
                "project_shared",
                "deleted decision",
                "can only back inactive role lenses",
                ProjectEventId) with
            {
                Status = MemoryFactStatuses.Deleted
            });
            var deletedLens = await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                ProjectScope(),
                "cto",
                deletedBaseFact.Id,
                "A deleted CTO lens should not be promoted while its base fact is deleted.",
                0.900m,
                ProjectEventId,
                PrincipalId,
                Status: MemoryFactStatuses.Deleted));

            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                UpdateRoleMemoryLensStatusAsync(databaseConnectionString, deletedLens.Id, MemoryFactStatuses.Active));

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("active memory facts", exception.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Database_rejects_deactivating_fact_referenced_by_active_role_lens()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_deactivate_base_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var projectBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                ProjectScope(),
                $"/project/{ProjectId}/decisions",
                "decision",
                "project_shared",
                "active decision",
                "backs an active role lens",
                ProjectEventId));
            await roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                ProjectScope(),
                "cto",
                projectBaseFact.Id,
                "The active CTO lens depends on this durable memory.",
                0.900m,
                ProjectEventId,
                PrincipalId));

            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                UpdateMemoryFactStatusAsync(databaseConnectionString, projectBaseFact.Id, MemoryFactStatuses.Deleted));

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("active role memory lens", exception.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task StoreAsync_rejects_source_event_outside_lens_scope()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_source_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var projectBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                ProjectScope(),
                $"/project/{ProjectId}/decisions",
                "decision",
                "project_shared",
                "lens source event validation",
                "requires scoped evidence",
                ProjectEventId));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                    ProjectScope(),
                    "cto",
                    projectBaseFact.Id,
                    "The source event must belong to the same project lens scope.",
                    0.900m,
                    OrgEventId,
                    PrincipalId)));

            Assert.Contains("does not exist for the role memory lens scope", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task StoreAsync_rejects_shared_role_principles_backed_by_project_fact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_shared_reject_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var projectBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                ProjectScope(),
                $"/project/{ProjectId}/decisions",
                "decision",
                "project_shared",
                "shared lens rejection decision",
                "project facts cannot back shared role principles",
                ProjectEventId));

            var globalException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                    GlobalScope(),
                    "cto",
                    projectBaseFact.Id,
                    "A global shared CTO principle cannot be backed by one project's decision.",
                    0.900m,
                    GlobalEventId,
                    PrincipalId)));
            var orgException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                    OrgScope(),
                    "cto",
                    projectBaseFact.Id,
                    "An organization shared CTO principle cannot be backed by one project's decision.",
                    0.900m,
                    OrgEventId,
                    PrincipalId)));

            Assert.Contains("Global role lenses must reference global memory facts", globalException.Message);
            Assert.Contains("Organization role lenses must reference memory facts from the same organization", orgException.Message);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task StoreAsync_rejects_project_role_lens_backed_by_global_or_other_project_fact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_role_memory_lens_project_reject_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await InsertProjectAsync(databaseConnectionString, OrgId, OtherProjectId, "Other Project");
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                OtherProjectEventId,
                PrincipalId,
                "project",
                OtherProjectId.ToString(),
                scopeOrgId: OrgId,
                scopeProjectId: OtherProjectId);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var memoryFacts = new PostgresMemoryFactRepository(dataSource);
            var roleLenses = new PostgresRoleMemoryLensRepository(dataSource);

            var globalBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                GlobalScope(),
                "/global/project-lens-rejection",
                "role_source",
                "role_shared",
                "global project lens rejection source",
                "global facts cannot back project role lenses",
                GlobalEventId));
            var otherProjectBaseFact = await memoryFacts.StoreAsync(CreateMemoryFactCommand(
                OtherProjectScope(),
                $"/project/{OtherProjectId}/decisions",
                "decision",
                "project_shared",
                "other project storage engine",
                "other project decisions cannot back this project lens",
                OtherProjectEventId));

            var globalException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                    ProjectScope(),
                    "cto",
                    globalBaseFact.Id,
                    "A project CTO lens cannot be backed by global truth.",
                    0.900m,
                    ProjectEventId,
                    PrincipalId)));
            var otherProjectException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                roleLenses.StoreAsync(new RoleMemoryLensWriteCommand(
                    ProjectScope(),
                    "cto",
                    otherProjectBaseFact.Id,
                    "A project CTO lens cannot be backed by a different project's truth.",
                    0.900m,
                    ProjectEventId,
                    PrincipalId)));

            Assert.Contains("Project role lenses must reference memory facts from the target project or its organization", globalException.Message);
            Assert.Contains("Project role lenses must reference memory facts from the target project or its organization", otherProjectException.Message);
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
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgId, ProjectId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            GlobalEventId,
            PrincipalId,
            "global",
            "global");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            OrgEventId,
            PrincipalId,
            "org",
            OrgId.ToString(),
            scopeOrgId: OrgId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectEventId,
            PrincipalId,
            "project",
            ProjectId.ToString(),
            scopeOrgId: OrgId,
            scopeProjectId: ProjectId);
    }

    private static async Task InsertProjectAsync(
        string connectionString,
        Guid orgId,
        Guid projectId,
        string projectName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO projects (id, org_id, name, status)
            VALUES (@project_id, @org_id, @project_name, 'active');
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("project_name", projectName);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpdateMemoryFactStatusAsync(
        string connectionString,
        Guid memoryFactId,
        string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE memory_facts
            SET status = @status
            WHERE id = @memory_fact_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpdateRoleMemoryLensStatusAsync(
        string connectionString,
        Guid roleMemoryLensId,
        string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE role_memory_lenses
            SET status = @status
            WHERE id = @role_memory_lens_id;
            """,
            connection);
        command.Parameters.AddWithValue("role_memory_lens_id", roleMemoryLensId);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ReadMemoryChunkContentAsync(
        string connectionString,
        Guid aggregateId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT content
            FROM memory_chunks
            WHERE source_type = 'role_memory_lens'
                AND source_id = @aggregate_id;
            """,
            connection);
        command.Parameters.AddWithValue("aggregate_id", aggregateId);

        return await command.ExecuteScalarAsync() as string
            ?? throw new InvalidOperationException($"Memory chunk for aggregate {aggregateId} was not found.");
    }

    private static MemoryFactWriteCommand CreateMemoryFactCommand(
        MemoryScopeResolution scope,
        string namespaceValue,
        string memoryType,
        string visibility,
        string subject,
        string objectValue,
        Guid sourceEventId)
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
            PrincipalId);
    }

    private static async Task AssertSingleScopedLensAsync(
        IRoleMemoryLensRepository repository,
        MemoryScopeResolution scope,
        Guid expectedRoleMemoryLensId)
    {
        var roleMemoryLenses = await repository.FindByScopeAsync(new RoleMemoryLensScopeQuery(scope, "cto"));
        var roleMemoryLens = Assert.Single(roleMemoryLenses);

        Assert.Equal(expectedRoleMemoryLensId, roleMemoryLens.Id);
        Assert.Equal("cto", roleMemoryLens.RoleId);
        Assert.Equal(scope.ScopeType, roleMemoryLens.ScopeType);
        Assert.Equal(scope.ScopeId, roleMemoryLens.ScopeId);
    }

    private static MemoryScopeResolution GlobalScope()
    {
        return new MemoryScopeResolution("global", "global");
    }

    private static MemoryScopeResolution OrgScope()
    {
        return new MemoryScopeResolution("org", OrgId.ToString(), OrgId: OrgId);
    }

    private static MemoryScopeResolution ProjectScope()
    {
        return new MemoryScopeResolution("project", ProjectId.ToString(), OrgId: OrgId, ProjectId: ProjectId);
    }

    private static MemoryScopeResolution OtherProjectScope()
    {
        return new MemoryScopeResolution("project", OtherProjectId.ToString(), OrgId: OrgId, ProjectId: OtherProjectId);
    }
}
