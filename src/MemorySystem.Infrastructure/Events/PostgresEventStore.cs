using System.Text.Json;
using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.DomainMapping;
using MemorySystem.Infrastructure.Idempotency;
using MemorySystem.Infrastructure.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Events;

public sealed class PostgresEventStore(NpgsqlDataSource dataSource) : IEventStore, IEventReadStore, ISourceEventReferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EventRecord?> FindAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                id,
                principal_id,
                conversation_id,
                agent_principal_id,
                role_id,
                event_type,
                content::text,
                content_hash,
                external_payload_uri,
                retention_class,
                sensitivity,
                trust_level,
                created_at,
                scope_type,
                scope_id,
                scope_org_id,
                scope_project_id,
                scope_principal_id,
                scope_role_id
            FROM events
            WHERE id = @event_id
                AND retention_class <> 'erasure_requested'
                AND redaction_status = 'none';
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        Guid? agentPrincipalId = reader.IsDBNull(3) ? null : reader.GetGuid(3);
        Guid? conversationId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
        Guid? scopeOrgId = reader.IsDBNull(15) ? null : reader.GetGuid(15);
        Guid? scopeProjectId = reader.IsDBNull(16) ? null : reader.GetGuid(16);
        Guid? scopePrincipalId = reader.IsDBNull(17) ? null : reader.GetGuid(17);
        var scopeRoleId = reader.IsDBNull(18) ? null : reader.GetString(18);
        var scope = PostgresDomainMapping.RequireScopeResolution(
            reader.GetString(13),
            reader.GetString(14),
            orgId: scopeOrgId,
            projectId: scopeProjectId,
            principalId: scopePrincipalId,
            roleId: scopeRoleId,
            scopeRoleId: scopeRoleId,
            conversationId: conversationId,
            agentPrincipalId: agentPrincipalId);

        return new EventRecord(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            conversationId,
            agentPrincipalId,
            PostgresDomainMapping.NormalizeOptionalRoleId(reader.IsDBNull(4) ? null : reader.GetString(4)),
            reader.GetString(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            PostgresDomainMapping.RequireRetentionClass(reader.GetString(9)),
            PostgresDomainMapping.RequireSensitivity(reader.GetString(10)),
            PostgresDomainMapping.RequireTrustLevel(reader.GetString(11)),
            reader.GetFieldValue<DateTimeOffset>(12),
            scope);
    }

    public async Task<SourceEventReference?> FindForPrincipalScopeAsync(
        Guid eventId,
        Guid principalId,
        string scopeType,
        string scopeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT id, trust_level, sensitivity
            FROM events
            WHERE id = @event_id
                AND COALESCE(principal_id, scope_principal_id, agent_principal_id, '00000000-0000-4000-8000-000000000007'::uuid) = @principal_id
                AND scope_type = @scope_type
                AND scope_id = @scope_id
                AND retention_class <> 'erasure_requested'
                AND redaction_status = 'none';
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("principal_id", principalId);
        var scope = PostgresDomainMapping.RequireScope(scopeType, scopeId);
        command.Parameters.AddWithValue("scope_type", scope.ScopeType);
        command.Parameters.AddWithValue("scope_id", scope.ScopeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? SourceEventReference.FromValues(reader.GetGuid(0), reader.GetString(1), reader.GetString(2))
            : null;
    }

    public async Task<AppendEventResult> AppendAsync(
        AppendEventCommand command,
        Guid idempotencyRecordId,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        var eventId = Guid.NewGuid();
        var scope = PostgresDomainMapping.RequireScope(command.Scope.Type, command.Scope.Id);
        var roleId = PostgresDomainMapping.NormalizeOptionalRoleId(command.RoleId);
        var scopeRoleId = PostgresDomainMapping.NormalizeOptionalRoleId(command.Scope.RoleId);
        var retentionClass = PostgresDomainMapping.RequireRetentionClass(command.RetentionClass);
        var sensitivity = PostgresDomainMapping.RequireSensitivity(command.Sensitivity);
        var trustLevel = PostgresDomainMapping.RequireTrustLevel(command.TrustLevel);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var scopeOrgId = scope.ScopeType == "project"
            ? await ResolveProjectOrgIdAsync(connection, transaction, command.Scope.ProjectId, cancellationToken)
            : command.Scope.OrgId;

        if (command.Scope.OrgId.HasValue
            && scopeOrgId.HasValue
            && command.Scope.OrgId.Value != scopeOrgId.Value)
        {
            throw new EventScopeNotFoundException(
                $"Project scope {command.Scope.ProjectId} does not belong to organization {command.Scope.OrgId}.");
        }

        const string sql = """
            INSERT INTO events (
                id,
                principal_id,
                conversation_id,
                agent_principal_id,
                role_id,
                event_type,
                content,
                content_hash,
                external_payload_uri,
                retention_class,
                sensitivity,
                trust_level,
                scope_type,
                scope_id,
                scope_org_id,
                scope_project_id,
                scope_principal_id,
                scope_role_id
            )
            VALUES (
                @id,
                @principal_id,
                @conversation_id,
                @agent_principal_id,
                @role_id,
                @event_type,
                @content,
                @content_hash,
                @external_payload_uri,
                @retention_class,
                @sensitivity,
                @trust_level,
                @scope_type,
                @scope_id,
                @scope_org_id,
                @scope_project_id,
                @scope_principal_id,
                @scope_role_id
            )
            RETURNING created_at;
            """;

        await using var insert = new NpgsqlCommand(sql, connection, transaction);
        insert.Parameters.AddWithValue("id", eventId);
        insert.Parameters.AddWithValue("principal_id", command.PrincipalId);
        insert.Parameters.AddWithValue("conversation_id", command.ConversationId.HasValue ? command.ConversationId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("agent_principal_id", command.AgentPrincipalId.HasValue ? command.AgentPrincipalId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("role_id", string.IsNullOrWhiteSpace(roleId) ? DBNull.Value : roleId);
        insert.Parameters.AddWithValue("event_type", command.EventType);
        insert.Parameters.Add("content", NpgsqlDbType.Jsonb).Value = command.ContentJson;
        insert.Parameters.AddWithValue("content_hash", command.ContentHash);
        insert.Parameters.AddWithValue("external_payload_uri", string.IsNullOrWhiteSpace(command.ExternalPayloadUri) ? DBNull.Value : command.ExternalPayloadUri);
        insert.Parameters.AddWithValue("retention_class", retentionClass);
        insert.Parameters.AddWithValue("sensitivity", sensitivity);
        insert.Parameters.AddWithValue("trust_level", trustLevel);
        insert.Parameters.AddWithValue("scope_type", scope.ScopeType);
        insert.Parameters.AddWithValue("scope_id", scope.ScopeId);
        insert.Parameters.AddWithValue("scope_org_id", scopeOrgId.HasValue ? scopeOrgId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("scope_project_id", command.Scope.ProjectId.HasValue ? command.Scope.ProjectId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("scope_principal_id", command.Scope.PrincipalId.HasValue ? command.Scope.PrincipalId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("scope_role_id", string.IsNullOrWhiteSpace(scopeRoleId) ? DBNull.Value : scopeRoleId);

        var createdAt = await insert.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Event append did not return a creation timestamp.");

        await PostgresApiIdempotencyCompleter.CompleteAsync(
            connection,
            transaction,
            idempotencyRecordId,
            requestHash,
            201,
            JsonSerializer.Serialize(new AppendEventResponseBody(eventId), JsonOptions),
            "application/json; charset=utf-8",
            "event",
            eventId,
            "The event idempotency record could not be completed.",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new AppendEventResult(eventId, ToDateTimeOffset(createdAt));
    }

    private static async Task<Guid> ResolveProjectOrgIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (!projectId.HasValue)
        {
            throw new InvalidOperationException("Project scope requires a project id.");
        }

        var project = await PostgresProjectScopeReader.FindAsync(
            connection,
            transaction,
            projectId.Value,
            cancellationToken);

        return project is not null
            ? project.OrgId
            : throw new EventScopeNotFoundException($"Project scope {projectId.Value} does not reference an existing project.");
    }

    private static DateTimeOffset ToDateTimeOffset(object value)
    {
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException($"Unexpected timestamp value '{value}'.")
        };
    }

    private sealed record AppendEventResponseBody(Guid Id);
}
