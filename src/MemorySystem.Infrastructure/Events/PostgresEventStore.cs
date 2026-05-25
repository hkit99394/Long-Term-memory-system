using System.Text.Json;
using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Idempotency;
using MemorySystem.Infrastructure.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Events;

public sealed class PostgresEventStore(NpgsqlDataSource dataSource) : IEventStore, ISourceEventReferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? new SourceEventReference(reader.GetGuid(0), reader.GetString(1), reader.GetString(2))
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

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var scopeOrgId = command.Scope.Type == "project"
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
        insert.Parameters.AddWithValue("role_id", string.IsNullOrWhiteSpace(command.RoleId) ? DBNull.Value : command.RoleId);
        insert.Parameters.AddWithValue("event_type", command.EventType);
        insert.Parameters.Add("content", NpgsqlDbType.Jsonb).Value = command.ContentJson;
        insert.Parameters.AddWithValue("content_hash", command.ContentHash);
        insert.Parameters.AddWithValue("external_payload_uri", string.IsNullOrWhiteSpace(command.ExternalPayloadUri) ? DBNull.Value : command.ExternalPayloadUri);
        insert.Parameters.AddWithValue("retention_class", command.RetentionClass);
        insert.Parameters.AddWithValue("sensitivity", command.Sensitivity);
        insert.Parameters.AddWithValue("trust_level", command.TrustLevel);
        insert.Parameters.AddWithValue("scope_type", command.Scope.Type);
        insert.Parameters.AddWithValue("scope_id", command.Scope.Id);
        insert.Parameters.AddWithValue("scope_org_id", scopeOrgId.HasValue ? scopeOrgId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("scope_project_id", command.Scope.ProjectId.HasValue ? command.Scope.ProjectId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("scope_principal_id", command.Scope.PrincipalId.HasValue ? command.Scope.PrincipalId.Value : DBNull.Value);
        insert.Parameters.AddWithValue("scope_role_id", string.IsNullOrWhiteSpace(command.Scope.RoleId) ? DBNull.Value : command.Scope.RoleId);

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
