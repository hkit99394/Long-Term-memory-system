using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryFacts;

public sealed class PostgresMemoryFactRepository(NpgsqlDataSource dataSource) : IMemoryFactRepository
{
    public async Task<MemoryFactRecord?> FindAsync(
        Guid memoryFactId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            $"""
            {SelectMemoryFactSql}
            WHERE id = @memory_fact_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? ReadMemoryFact(reader)
            : null;
    }

    public async Task<IReadOnlyList<MemoryFactRecord>> FindByScopeAsync(
        MemoryFactScopeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await SearchAsync(
            new MemoryFactSearchQuery(
                query.Scope,
                query.MemoryType,
                Status: query.Status,
                Limit: query.Limit),
            cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryFactRecord>> SearchAsync(
        MemoryFactSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Status);

        if (!MemoryFactStatuses.IsSupported(query.Status))
        {
            throw new ArgumentException($"Memory fact status '{query.Status}' is not supported.", nameof(query));
        }

        if (query.Limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Memory fact query limit must be between 1 and 500.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            $"""
            {SelectMemoryFactSql}
            WHERE scope_type = @scope_type
                AND scope_id = @scope_id
                AND status = @status
                AND (@memory_type IS NULL OR memory_type = @memory_type)
                AND (@subject IS NULL OR strpos(lower(subject), lower(@subject)) > 0)
            ORDER BY updated_at DESC, created_at DESC, id
            LIMIT @limit;
            """,
            connection);
        AddScopeParameters(command, query.Scope);
        command.Parameters.AddWithValue("status", query.Status);
        command.Parameters.Add("memory_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.MemoryType) ? DBNull.Value : query.MemoryType;
        command.Parameters.Add("subject", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.Subject) ? DBNull.Value : query.Subject;
        command.Parameters.AddWithValue("limit", query.Limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var memoryFacts = new List<MemoryFactRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            memoryFacts.Add(ReadMemoryFact(reader));
        }

        return memoryFacts;
    }

    public async Task<MemoryFactRecord> StoreAsync(
        MemoryFactWriteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Namespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.MemoryType);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Visibility);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Object);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Status);

        if (!MemoryFactStatuses.IsSupported(command.Status))
        {
            throw new ArgumentException($"Memory fact status '{command.Status}' is not supported.", nameof(command));
        }

        if (command.Confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(command), command.Confidence, "Memory fact confidence must be between 0 and 1.");
        }

        var memoryFactId = command.Id ?? Guid.NewGuid();
        var ownerColumns = ResolveOwnerColumns(command.Scope);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var insert = new NpgsqlCommand(
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
                @memory_type,
                @visibility,
                @subject,
                @predicate,
                @object,
                @confidence,
                @status,
                @source_event_id,
                @proposed_by_principal_id
            );
            """,
            connection);
        insert.Parameters.AddWithValue("id", memoryFactId);
        AddScopeParameters(insert, command.Scope);
        insert.Parameters.AddWithValue("namespace", command.Namespace);
        AddOwnerParameters(insert, ownerColumns);
        insert.Parameters.AddWithValue("memory_type", command.MemoryType);
        insert.Parameters.AddWithValue("visibility", command.Visibility);
        insert.Parameters.AddWithValue("subject", command.Subject);
        insert.Parameters.AddWithValue("predicate", command.Predicate);
        insert.Parameters.AddWithValue("object", command.Object);
        insert.Parameters.AddWithValue("confidence", command.Confidence);
        insert.Parameters.AddWithValue("status", command.Status);
        insert.Parameters.AddWithValue("source_event_id", command.SourceEventId);
        insert.Parameters.AddWithValue("proposed_by_principal_id", command.ProposedByPrincipalId);

        await insert.ExecuteNonQueryAsync(cancellationToken);

        var memoryFact = await FindAsync(memoryFactId, cancellationToken);

        return memoryFact
            ?? throw new InvalidOperationException($"Stored memory fact {memoryFactId} could not be read back.");
    }

    private const string SelectMemoryFactSql = """
        SELECT
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
            status,
            source_event_id,
            proposed_by_principal_id
        FROM memory_facts
        """;

    private static MemoryFactRecord ReadMemoryFact(NpgsqlDataReader reader)
    {
        return new MemoryFactRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetString(12),
            reader.GetString(13),
            reader.GetDecimal(14),
            reader.GetString(15),
            reader.GetGuid(16),
            reader.IsDBNull(17) ? null : reader.GetGuid(17));
    }

    private static ScopeOwnerColumns ResolveOwnerColumns(MemoryScopeResolution scope)
    {
        return scope.ScopeType switch
        {
            "global" => new ScopeOwnerColumns(),
            "org" => new ScopeOwnerColumns(OrgId: Require(scope.OrgId, "Organization scope requires an organization id.")),
            "user" => new ScopeOwnerColumns(UserPrincipalId: Require(scope.PrincipalId, "User scope requires a principal id.")),
            "project" => new ScopeOwnerColumns(
                ProjectId: Require(scope.ProjectId, "Project scope requires a project id."),
                OrgId: Require(scope.OrgId, "Project scope requires an organization id.")),
            "role" => new ScopeOwnerColumns(RoleId: Require(scope.ScopeRoleId ?? scope.RoleId, "Role scope requires a role id.")),
            "agent" => new ScopeOwnerColumns(AgentPrincipalId: Require(scope.AgentPrincipalId, "Agent scope requires an agent principal id.")),
            "session" => new ScopeOwnerColumns(),
            _ => throw new InvalidOperationException($"Unsupported memory fact scope type '{scope.ScopeType}'.")
        };
    }

    private static Guid Require(Guid? value, string message)
    {
        return value ?? throw new InvalidOperationException(message);
    }

    private static string Require(string? value, string message)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(message)
            : value;
    }

    private static void AddScopeParameters(NpgsqlCommand command, MemoryScopeResolution scope)
    {
        command.Parameters.AddWithValue("scope_type", scope.ScopeType);
        command.Parameters.AddWithValue("scope_id", scope.ScopeId);
    }

    private static void AddOwnerParameters(NpgsqlCommand command, ScopeOwnerColumns ownerColumns)
    {
        command.Parameters.Add("user_principal_id", NpgsqlDbType.Uuid).Value =
            ownerColumns.UserPrincipalId.HasValue ? ownerColumns.UserPrincipalId.Value : DBNull.Value;
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            ownerColumns.ProjectId.HasValue ? ownerColumns.ProjectId.Value : DBNull.Value;
        command.Parameters.Add("org_id", NpgsqlDbType.Uuid).Value =
            ownerColumns.OrgId.HasValue ? ownerColumns.OrgId.Value : DBNull.Value;
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(ownerColumns.RoleId) ? DBNull.Value : ownerColumns.RoleId;
        command.Parameters.Add("agent_principal_id", NpgsqlDbType.Uuid).Value =
            ownerColumns.AgentPrincipalId.HasValue ? ownerColumns.AgentPrincipalId.Value : DBNull.Value;
    }

    private sealed record ScopeOwnerColumns(
        Guid? UserPrincipalId = null,
        Guid? ProjectId = null,
        Guid? OrgId = null,
        string? RoleId = null,
        Guid? AgentPrincipalId = null);
}
