using System.Text.Json;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Idempotency;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Events;

public sealed class PostgresEventStore(NpgsqlDataSource dataSource) : IEventStore, ISourceEventReferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> ExistsForPrincipalScopeAsync(
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
            SELECT EXISTS (
                SELECT 1
                FROM events
                WHERE id = @event_id
                    AND principal_id = @principal_id
                    AND scope_type = @scope_type
                    AND scope_id = @scope_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);

        return await command.ExecuteScalarAsync(cancellationToken) is true;
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

        await PostgresTransactionalIdempotencyCompleter.CompleteAsync(
            connection,
            transaction,
            idempotencyRecordId,
            requestHash,
            201,
            JsonSerializer.Serialize(new AppendEventResponseBody(eventId), JsonOptions),
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

        await using var command = new NpgsqlCommand(
            "SELECT org_id FROM projects WHERE id = @project_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("project_id", projectId.Value);

        var orgId = await command.ExecuteScalarAsync(cancellationToken);

        return orgId is Guid value
            ? value
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
