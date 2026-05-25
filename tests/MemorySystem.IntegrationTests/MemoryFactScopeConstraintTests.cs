using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class MemoryFactScopeConstraintTests
{
    private const string ScopeConsistencyConstraint = "ck_memory_facts_scope_owner_namespace_consistency";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OtherPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid SourceEventId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_facts_reject_owner_column_drift_on_update()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_fact_owner_drift_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            var memoryId = await InsertMemoryFactAsync(
                databaseConnectionString,
                scopeType: "user",
                scopeId: PrincipalId.ToString(),
                namespaceValue: $"/user/{PrincipalId}/preferences",
                userPrincipalId: PrincipalId);

            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => UpdateUserOwnerAsync(databaseConnectionString, memoryId, OtherPrincipalId));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_facts_reject_session_namespace_prefix_drift_without_like_wildcards()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_fact_namespace_drift_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString, scopeType: "session", scopeId: "session%1");

            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => InsertMemoryFactAsync(
                    databaseConnectionString,
                    scopeType: "session",
                    scopeId: "session%1",
                    namespaceValue: "/session/sessionXYZ1/instructions"));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal(ScopeConsistencyConstraint, exception.ConstraintName);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task PrepareDatabaseAsync(
        string connectionString,
        string scopeType = "user",
        string? scopeId = null)
    {
        var resolvedScopeId = scopeId ?? PrincipalId.ToString();

        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, OtherPrincipalId, displayName: "Other Principal");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            SourceEventId,
            PrincipalId,
            scopeType,
            resolvedScopeId);
    }

    private static async Task<Guid> InsertMemoryFactAsync(
        string connectionString,
        string scopeType,
        string scopeId,
        string namespaceValue,
        Guid? userPrincipalId = null,
        Guid? projectId = null,
        Guid? orgId = null,
        string? roleId = null,
        Guid? agentPrincipalId = null)
    {
        var memoryId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_facts (
                id,
                scope_type,
                scope_id,
                namespace,
                user_principal_id,
                project_id,
                org_id,
                role_id,
                agent_principal_id,
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
                @scope_type,
                @scope_id,
                @namespace,
                @user_principal_id,
                @project_id,
                @org_id,
                @role_id,
                @agent_principal_id,
                'preference',
                'private',
                'technical planning format',
                'prefers',
                'concise decision logs',
                0.950,
                'user_scoped',
                'active',
                @source_event_id,
                @proposed_by_principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", memoryId);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.AddWithValue("namespace", namespaceValue);
        command.Parameters.Add("user_principal_id", NpgsqlDbType.Uuid).Value =
            userPrincipalId.HasValue ? userPrincipalId.Value : DBNull.Value;
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            projectId.HasValue ? projectId.Value : DBNull.Value;
        command.Parameters.Add("org_id", NpgsqlDbType.Uuid).Value =
            orgId.HasValue ? orgId.Value : DBNull.Value;
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(roleId) ? DBNull.Value : roleId;
        command.Parameters.Add("agent_principal_id", NpgsqlDbType.Uuid).Value =
            agentPrincipalId.HasValue ? agentPrincipalId.Value : DBNull.Value;
        command.Parameters.AddWithValue("source_event_id", SourceEventId);
        command.Parameters.AddWithValue("proposed_by_principal_id", PrincipalId);

        await command.ExecuteNonQueryAsync();

        return memoryId;
    }

    private static async Task UpdateUserOwnerAsync(
        string connectionString,
        Guid memoryId,
        Guid userPrincipalId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE memory_facts
            SET user_principal_id = @user_principal_id
            WHERE id = @memory_id;
            """,
            connection);
        command.Parameters.AddWithValue("user_principal_id", userPrincipalId);
        command.Parameters.AddWithValue("memory_id", memoryId);

        await command.ExecuteNonQueryAsync();
    }
}
