using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.RoleMemoryLenses;
using MemorySystem.Application.Scopes;
using Npgsql;
using NpgsqlTypes;

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

        var lensScope = ResolveLensScope(query.Scope);

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
        AddScopeParameters(command, lensScope);
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

        if (!MemoryFactStatuses.IsSupported(command.Status))
        {
            throw new ArgumentException($"Role memory lens status '{command.Status}' is not supported.", nameof(command));
        }

        if (command.Confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(command), command.Confidence, "Role memory lens confidence must be between 0 and 1.");
        }

        var roleMemoryLensId = command.Id ?? Guid.NewGuid();
        var lensScope = ResolveLensScope(command.Scope);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await ValidateBaseMemoryFactScopeAsync(
            connection,
            lensScope,
            command.BaseMemoryFactId,
            command.Status,
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
                source_event_id
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
                @source_event_id
            );
            """,
            connection);
        insert.Parameters.AddWithValue("id", roleMemoryLensId);
        insert.Parameters.AddWithValue("role_id", command.RoleId);
        AddScopeParameters(insert, lensScope);
        AddOwnerParameters(insert, lensScope);
        insert.Parameters.AddWithValue("base_memory_fact_id", command.BaseMemoryFactId);
        insert.Parameters.AddWithValue("interpretation", command.Interpretation);
        insert.Parameters.AddWithValue("confidence", command.Confidence);
        insert.Parameters.AddWithValue("status", command.Status);
        insert.Parameters.AddWithValue("source_event_id", command.SourceEventId);

        await insert.ExecuteNonQueryAsync(cancellationToken);

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
            source_event_id
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
            reader.GetGuid(10));
    }

    private static async Task ValidateBaseMemoryFactScopeAsync(
        NpgsqlConnection connection,
        LensScopeColumns lensScope,
        Guid baseMemoryFactId,
        string lensStatus,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT scope_type, org_id, project_id, status
            FROM memory_facts
            WHERE id = @base_memory_fact_id;
            """,
            connection);
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

        if (!IsValidBaseMemoryFactScope(lensScope, baseScope))
        {
            throw new InvalidOperationException(BuildInvalidBaseScopeMessage(lensScope));
        }
    }

    private static bool IsValidBaseMemoryFactScope(
        LensScopeColumns lensScope,
        BaseMemoryFactScope baseScope)
    {
        return lensScope.ScopeType switch
        {
            "global" => baseScope.ScopeType == "global",
            "org" => baseScope.ScopeType == "org"
                && baseScope.OrgId == lensScope.OrgId,
            "project" => (baseScope.ScopeType == "project" && baseScope.ProjectId == lensScope.ProjectId)
                || (baseScope.ScopeType == "org" && baseScope.OrgId == lensScope.OrgId),
            _ => false
        };
    }

    private static string BuildInvalidBaseScopeMessage(LensScopeColumns lensScope)
    {
        return lensScope.ScopeType switch
        {
            "global" => "Global role lenses must reference global memory facts.",
            "org" => "Organization role lenses must reference memory facts from the same organization.",
            "project" => "Project role lenses must reference memory facts from the target project or its organization.",
            _ => $"Unsupported role memory lens scope type '{lensScope.ScopeType}'."
        };
    }

    private static LensScopeColumns ResolveLensScope(MemoryScopeResolution scope)
    {
        return scope.ScopeType switch
        {
            "global" when scope.ScopeId == "global" => new LensScopeColumns("global", "global"),
            "org" => new LensScopeColumns(
                "org",
                Require(scope.OrgId, "Organization role lens scope requires an organization id.").ToString(),
                OrgId: Require(scope.OrgId, "Organization role lens scope requires an organization id.")),
            "project" => new LensScopeColumns(
                "project",
                Require(scope.ProjectId, "Project role lens scope requires a project id.").ToString(),
                OrgId: Require(scope.OrgId, "Project role lens scope requires an organization id."),
                ProjectId: Require(scope.ProjectId, "Project role lens scope requires a project id.")),
            "global" => throw new InvalidOperationException("Global role lens scope requires scope id 'global'."),
            _ => throw new InvalidOperationException($"Unsupported role memory lens scope type '{scope.ScopeType}'.")
        };
    }

    private static Guid Require(Guid? value, string message)
    {
        return value ?? throw new InvalidOperationException(message);
    }

    private static void AddScopeParameters(NpgsqlCommand command, LensScopeColumns lensScope)
    {
        command.Parameters.AddWithValue("scope_type", lensScope.ScopeType);
        command.Parameters.AddWithValue("scope_id", lensScope.ScopeId);
    }

    private static void AddOwnerParameters(NpgsqlCommand command, LensScopeColumns lensScope)
    {
        command.Parameters.Add("org_id", NpgsqlDbType.Uuid).Value =
            lensScope.OrgId.HasValue ? lensScope.OrgId.Value : DBNull.Value;
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            lensScope.ProjectId.HasValue ? lensScope.ProjectId.Value : DBNull.Value;
    }

    private sealed record LensScopeColumns(
        string ScopeType,
        string ScopeId,
        Guid? OrgId = null,
        Guid? ProjectId = null);

    private sealed record BaseMemoryFactScope(
        string ScopeType,
        Guid? OrgId,
        Guid? ProjectId,
        string Status);
}
