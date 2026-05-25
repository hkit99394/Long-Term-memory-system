using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.RoleMemoryLenses;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.Outbox;
using Npgsql;

namespace MemorySystem.Infrastructure.RoleMemoryLenses;

public sealed class PostgresRoleMemoryLensRepository(NpgsqlDataSource dataSource) : IRoleMemoryLensRepository
{
    public async Task<RoleMemoryLensRecord?> FindAsync(
        Guid roleMemoryLensId,
        CancellationToken cancellationToken = default)
    {
        if (roleMemoryLensId == Guid.Empty)
        {
            throw new ArgumentException("Role memory lens id must not be empty.", nameof(roleMemoryLensId));
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            $"""
            {SelectRoleMemoryLensSql}
            WHERE id = @role_memory_lens_id;
            """,
            connection);
        command.Parameters.AddWithValue("role_memory_lens_id", roleMemoryLensId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? ReadRoleMemoryLens(reader)
            : null;
    }

    public async Task<IReadOnlyList<RoleMemoryLensRecord>> FindByScopeAsync(
        RoleMemoryLensScopeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.RoleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Status);

        if (!MemoryScopePolicy.RoleIds.Contains(query.RoleId))
        {
            throw new ArgumentException($"Role id '{query.RoleId}' is not supported.", nameof(query));
        }

        if (!MemoryFactStatuses.IsSupported(query.Status))
        {
            throw new ArgumentException($"Role memory lens status '{query.Status}' is not supported.", nameof(query));
        }

        if (query.Limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Role memory lens query limit must be between 1 and 500.");
        }

        var lensScope = RoleMemoryLensStorageRules.ResolveLensScope(query.Scope);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            $"""
            {SelectRoleMemoryLensSql}
            WHERE role_id = @role_id
                AND scope_type = @scope_type
                AND scope_id = @scope_id
                AND status = @status
            ORDER BY updated_at DESC, created_at DESC, id
            LIMIT @limit;
            """,
            connection);
        command.Parameters.AddWithValue("role_id", query.RoleId);
        RoleMemoryLensStorageRules.AddScopeParameters(command, lensScope);
        command.Parameters.AddWithValue("status", query.Status);
        command.Parameters.AddWithValue("limit", query.Limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lenses = new List<RoleMemoryLensRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            lenses.Add(ReadRoleMemoryLens(reader));
        }

        return lenses;
    }

    public async Task<RoleMemoryLensRecord> StoreAsync(
        RoleMemoryLensWriteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.RoleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Interpretation);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Status);

        if (!MemoryScopePolicy.RoleIds.Contains(command.RoleId))
        {
            throw new ArgumentException($"Role id '{command.RoleId}' is not supported.", nameof(command));
        }

        if (command.BaseMemoryFactId == Guid.Empty)
        {
            throw new ArgumentException("Base memory fact id must not be empty.", nameof(command));
        }

        if (command.SourceEventId == Guid.Empty)
        {
            throw new ArgumentException("Source event id must not be empty.", nameof(command));
        }

        if (command.ProposedByPrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Proposed-by principal id must not be empty.", nameof(command));
        }

        if (!MemoryFactStatuses.IsSupported(command.Status))
        {
            throw new ArgumentException($"Role memory lens status '{command.Status}' is not supported.", nameof(command));
        }

        if (command.Confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(command), command.Confidence, "Role memory lens confidence must be between 0 and 1.");
        }

        var roleMemoryLensId = command.Id ?? Guid.NewGuid();
        var lensScope = RoleMemoryLensStorageRules.ResolveLensScope(command.Scope);
        var interpretation = command.Interpretation.Trim();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ValidateBaseMemoryFactScopeAsync(
            connection,
            transaction,
            lensScope,
            command.BaseMemoryFactId,
            command.Status,
            cancellationToken);
        var trustLevel = await ValidateSourceEventScopeAsync(
            connection,
            transaction,
            lensScope,
            command.SourceEventId,
            command.ProposedByPrincipalId,
            cancellationToken);

        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO role_memory_lenses (
                id,
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
            )
            VALUES (
                @id,
                @role_id,
                @scope_type,
                @scope_id,
                @org_id,
                @project_id,
                @base_memory_fact_id,
                @interpretation,
                @confidence,
                @status,
                @source_event_id,
                @proposed_by_principal_id
            );
            """,
            connection,
            transaction);
        insert.Parameters.AddWithValue("id", roleMemoryLensId);
        insert.Parameters.AddWithValue("role_id", command.RoleId);
        RoleMemoryLensStorageRules.AddScopeParameters(insert, lensScope);
        RoleMemoryLensStorageRules.AddOwnerParameters(insert, lensScope);
        insert.Parameters.AddWithValue("base_memory_fact_id", command.BaseMemoryFactId);
        insert.Parameters.AddWithValue("interpretation", interpretation);
        insert.Parameters.AddWithValue("confidence", command.Confidence);
        insert.Parameters.AddWithValue("status", command.Status);
        insert.Parameters.AddWithValue("source_event_id", command.SourceEventId);
        insert.Parameters.AddWithValue("proposed_by_principal_id", command.ProposedByPrincipalId);

        await insert.ExecuteNonQueryAsync(cancellationToken);

        if (MemoryFactStatuses.IsNormalRetrievalStatus(command.Status))
        {
            var chunkScope = RoleMemoryLensStorageRules.ResolveChunkScope(lensScope, command.RoleId);
            var chunkId = Guid.NewGuid();
            await MemoryIndexWriteOperations.InsertMemoryChunkAsync(
                connection,
                transaction,
                chunkId,
                MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType,
                roleMemoryLensId,
                chunkScope.Namespace,
                chunkScope.ScopeType,
                chunkScope.ScopeId,
                command.RoleId,
                interpretation,
                trustLevel,
                command.SourceEventId,
                cancellationToken);
            await MemoryIndexWriteOperations.InsertOutboxJobAsync(
                connection,
                transaction,
                Guid.NewGuid(),
                MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType,
                roleMemoryLensId,
                chunkId,
                command.SourceEventId,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var roleMemoryLens = await FindAsync(roleMemoryLensId, cancellationToken);

        return roleMemoryLens
            ?? throw new InvalidOperationException($"Stored role memory lens {roleMemoryLensId} could not be read back.");
    }

    private const string SelectRoleMemoryLensSql = """
        SELECT
            id,
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
        """;

    private static RoleMemoryLensRecord ReadRoleMemoryLens(NpgsqlDataReader reader)
    {
        return new RoleMemoryLensRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.GetGuid(6),
            reader.GetString(7),
            reader.GetDecimal(8),
            reader.GetString(9),
            reader.GetGuid(10),
            reader.GetGuid(11));
    }

    private static async Task ValidateBaseMemoryFactScopeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RoleMemoryLensStorageScope lensScope,
        Guid baseMemoryFactId,
        string lensStatus,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT scope_type, org_id, project_id, status
            FROM memory_facts
            WHERE id = @base_memory_fact_id
            FOR UPDATE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("base_memory_fact_id", baseMemoryFactId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Base memory fact {baseMemoryFactId} does not exist.");
        }

        var baseScope = new BaseMemoryFactScope(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.GetString(3));

        if (string.Equals(lensStatus, MemoryFactStatuses.Active, StringComparison.Ordinal)
            && !string.Equals(baseScope.Status, MemoryFactStatuses.Active, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active role memory lenses must reference active memory facts.");
        }

        if (!RoleMemoryLensStorageRules.IsValidBaseMemoryFactScope(
            lensScope,
            baseScope.ScopeType,
            baseScope.OrgId,
            baseScope.ProjectId))
        {
            throw new InvalidOperationException(RoleMemoryLensStorageRules.BuildInvalidBaseScopeMessage(lensScope));
        }
    }

    private static async Task<string> ValidateSourceEventScopeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RoleMemoryLensStorageScope lensScope,
        Guid sourceEventId,
        Guid proposedByPrincipalId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT trust_level
            FROM events
            WHERE id = @source_event_id
                AND COALESCE(principal_id, scope_principal_id, agent_principal_id, '00000000-0000-4000-8000-000000000007'::uuid) = @principal_id
                AND scope_type = @scope_type
                AND scope_id = @scope_id
                AND retention_class <> 'erasure_requested'
                AND redaction_status = 'none';
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("principal_id", proposedByPrincipalId);
        RoleMemoryLensStorageRules.AddScopeParameters(command, lensScope);

        return await command.ExecuteScalarAsync(cancellationToken) as string
            ?? throw new InvalidOperationException(
                $"Source event {sourceEventId} does not exist for the role memory lens scope.");
    }

    private sealed record BaseMemoryFactScope(
        string ScopeType,
        Guid? OrgId,
        Guid? ProjectId,
        string Status);
}
