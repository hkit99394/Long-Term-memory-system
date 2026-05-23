using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Idempotency;
using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Infrastructure.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryProposals;

public sealed class PostgresMemoryProposalWriteStore(NpgsqlDataSource dataSource) : IMemoryProposalWriteStore
{
    private const string ActiveMemoryDedupeIndexName = "ux_memory_facts_active_dedupe";
    private const string MemoryFactInsertSavepoint = "memory_fact_insert";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MemoryProposalDecision> StoreAsync(
        Guid proposedByPrincipalId,
        MemoryProposalCommand proposal,
        Guid idempotencyRecordId,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        if (!proposal.SourceEventId.HasValue)
        {
            throw new InvalidOperationException("Stored memory proposals require a source event id.");
        }

        if (!proposal.Confidence.HasValue)
        {
            throw new InvalidOperationException("Stored memory proposals require confidence.");
        }

        var memoryId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var outboxJobId = Guid.NewGuid();
        var chunkContent = BuildChunkContent(proposal);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var ownerColumns = await ResolveOwnerColumnsAsync(
            connection,
            transaction,
            proposal.ScopeType,
            proposal.ScopeId,
            cancellationToken);

        await ExecuteTransactionCommandAsync(connection, transaction, $"SAVEPOINT {MemoryFactInsertSavepoint};", cancellationToken);

        try
        {
            await InsertMemoryFactAsync(
                connection,
                transaction,
                memoryId,
                proposedByPrincipalId,
                proposal,
                ownerColumns,
                cancellationToken);
            await ExecuteTransactionCommandAsync(connection, transaction, $"RELEASE SAVEPOINT {MemoryFactInsertSavepoint};", cancellationToken);
        }
        catch (PostgresException exception) when (IsActiveMemoryDedupeViolation(exception))
        {
            await ExecuteTransactionCommandAsync(connection, transaction, $"ROLLBACK TO SAVEPOINT {MemoryFactInsertSavepoint};", cancellationToken);
            await ExecuteTransactionCommandAsync(connection, transaction, $"RELEASE SAVEPOINT {MemoryFactInsertSavepoint};", cancellationToken);

            var existingMemoryId = await FindDuplicateActiveMemoryFactAsync(
                connection,
                transaction,
                proposal,
                cancellationToken);
            var duplicateDecision = new MemoryProposalDecision(
                MemoryProposalDecisions.Stored,
                "The proposal matched existing durable memory.",
                existingMemoryId,
                proposal.SourceEventId);

            await CompleteIdempotencyAsync(
                connection,
                transaction,
                idempotencyRecordId,
                requestHash,
                duplicateDecision,
                "memory_fact",
                existingMemoryId,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return duplicateDecision;
        }

        await InsertMemoryChunkAsync(
            connection,
            transaction,
            chunkId,
            memoryId,
            proposal,
            chunkContent,
            cancellationToken);
        await InsertOutboxJobAsync(
            connection,
            transaction,
            outboxJobId,
            memoryId,
            chunkId,
            proposal.SourceEventId.Value,
            cancellationToken);
        var decision = new MemoryProposalDecision(
            MemoryProposalDecisions.Stored,
            "The proposal was stored as durable memory.",
            memoryId,
            proposal.SourceEventId);
        await CompleteIdempotencyAsync(
            connection,
            transaction,
            idempotencyRecordId,
            requestHash,
            decision,
            "memory_fact",
            memoryId,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return decision;
    }

    private static bool IsActiveMemoryDedupeViolation(PostgresException exception)
    {
        return exception.SqlState == PostgresErrorCodes.UniqueViolation
            && (
                string.Equals(exception.ConstraintName, ActiveMemoryDedupeIndexName, StringComparison.Ordinal)
                || exception.MessageText.Contains(ActiveMemoryDedupeIndexName, StringComparison.Ordinal)
            );
    }

    private static async Task ExecuteTransactionCommandAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CompleteIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid idempotencyRecordId,
        string requestHash,
        MemoryProposalDecision decision,
        string resourceType,
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        await PostgresApiIdempotencyCompleter.CompleteAsync(
            connection,
            transaction,
            idempotencyRecordId,
            requestHash,
            200,
            JsonSerializer.Serialize(decision, JsonOptions),
            resourceType,
            resourceId,
            "The proposal idempotency record could not be completed.",
            cancellationToken);
    }

    private static async Task<ScopeOwnerColumns> ResolveOwnerColumnsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string scopeType,
        string scopeId,
        CancellationToken cancellationToken)
    {
        return scopeType switch
        {
            "global" => new ScopeOwnerColumns(),
            "org" => new ScopeOwnerColumns(OrgId: Guid.Parse(scopeId)),
            "user" => new ScopeOwnerColumns(UserPrincipalId: Guid.Parse(scopeId)),
            "agent" => new ScopeOwnerColumns(AgentPrincipalId: Guid.Parse(scopeId)),
            "role" => new ScopeOwnerColumns(RoleId: scopeId),
            "session" => new ScopeOwnerColumns(),
            "project" => await ResolveProjectOwnerColumnsAsync(connection, transaction, scopeId, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported memory proposal scope type '{scopeType}'.")
        };
    }

    private static async Task<ScopeOwnerColumns> ResolveProjectOwnerColumnsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string scopeId,
        CancellationToken cancellationToken)
    {
        var projectId = Guid.Parse(scopeId);
        var project = await PostgresProjectScopeReader.FindAsync(
            connection,
            transaction,
            projectId,
            cancellationToken);

        if (project is null)
        {
            throw new InvalidOperationException($"Project scope {projectId} does not reference an existing project.");
        }

        return new ScopeOwnerColumns(
            ProjectId: projectId,
            OrgId: project.OrgId);
    }

    private static async Task InsertMemoryFactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid memoryId,
        Guid proposedByPrincipalId,
        MemoryProposalCommand proposal,
        ScopeOwnerColumns ownerColumns,
        CancellationToken cancellationToken)
    {
        const string sql = """
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
                @memory_type,
                @visibility,
                @subject,
                @predicate,
                @object,
                @confidence,
                @trust_level,
                'active',
                @source_event_id,
                @proposed_by_principal_id
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", memoryId);
        AddProposalParameters(command, proposal);
        AddOwnerParameters(command, ownerColumns);
        command.Parameters.AddWithValue("confidence", proposal.Confidence!.Value);
        command.Parameters.AddWithValue("trust_level", proposal.TrustLevel);
        command.Parameters.AddWithValue("source_event_id", proposal.SourceEventId!.Value);
        command.Parameters.AddWithValue("proposed_by_principal_id", proposedByPrincipalId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> FindDuplicateActiveMemoryFactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MemoryProposalCommand proposal,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id
            FROM memory_facts
            WHERE status = 'active'
                AND scope_type = @scope_type
                AND scope_id = @scope_id
                AND memory_type = @memory_type
                AND lower(subject) = lower(@subject)
                AND lower(predicate) = lower(@predicate)
                AND md5(object) = md5(@object)
            ORDER BY created_at, id
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddProposalParameters(command, proposal);

        return await command.ExecuteScalarAsync(cancellationToken) is Guid memoryId
            ? memoryId
            : throw new InvalidOperationException("Duplicate active memory fact was not found after dedupe conflict.");
    }

    private static async Task InsertMemoryChunkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid chunkId,
        Guid memoryId,
        MemoryProposalCommand proposal,
        string chunkContent,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO memory_chunks (
                id,
                source_type,
                source_id,
                namespace,
                scope_type,
                scope_id,
                title,
                content,
                content_hash,
                trust_level,
                source_event_id
            )
            VALUES (
                @id,
                @source_type,
                @source_id,
                @namespace,
                @scope_type,
                @scope_id,
                @title,
                @content,
                @content_hash,
                @trust_level,
                @source_event_id
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", chunkId);
        command.Parameters.AddWithValue("source_type", MemoryIndexOutboxJobContract.AggregateType);
        command.Parameters.AddWithValue("source_id", memoryId);
        command.Parameters.AddWithValue("namespace", proposal.Namespace);
        command.Parameters.AddWithValue("scope_type", proposal.ScopeType);
        command.Parameters.AddWithValue("scope_id", proposal.ScopeId);
        command.Parameters.AddWithValue("title", proposal.Subject);
        command.Parameters.AddWithValue("content", chunkContent);
        command.Parameters.AddWithValue("content_hash", ComputeSha256(chunkContent));
        command.Parameters.AddWithValue("trust_level", proposal.TrustLevel);
        command.Parameters.AddWithValue("source_event_id", proposal.SourceEventId!.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOutboxJobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid outboxJobId,
        Guid memoryId,
        Guid chunkId,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO outbox_jobs (
                id,
                job_type,
                aggregate_type,
                aggregate_id,
                idempotency_key,
                payload,
                status
            )
            VALUES (
                @id,
                @job_type,
                @aggregate_type,
                @aggregate_id,
                @idempotency_key,
                @payload,
                'pending'
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", outboxJobId);
        command.Parameters.AddWithValue("job_type", MemoryIndexOutboxJobContract.JobType);
        command.Parameters.AddWithValue("aggregate_type", MemoryIndexOutboxJobContract.AggregateType);
        command.Parameters.AddWithValue("aggregate_id", memoryId);
        command.Parameters.AddWithValue("idempotency_key", MemoryIndexOutboxJobContract.CreateIdempotencyKey(memoryId));
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value =
            MemoryIndexOutboxJobContract.SerializePayload(memoryId, chunkId, sourceEventId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddProposalParameters(NpgsqlCommand command, MemoryProposalCommand proposal)
    {
        command.Parameters.AddWithValue("scope_type", proposal.ScopeType);
        command.Parameters.AddWithValue("scope_id", proposal.ScopeId);
        command.Parameters.AddWithValue("namespace", proposal.Namespace);
        command.Parameters.AddWithValue("memory_type", proposal.MemoryType);
        command.Parameters.AddWithValue("visibility", proposal.Visibility);
        command.Parameters.AddWithValue("subject", proposal.Subject);
        command.Parameters.AddWithValue("predicate", proposal.Predicate);
        command.Parameters.AddWithValue("object", proposal.Object);
    }

    private static void AddOwnerParameters(NpgsqlCommand command, ScopeOwnerColumns ownerColumns)
    {
        command.Parameters.AddWithValue("user_principal_id", ownerColumns.UserPrincipalId.HasValue ? ownerColumns.UserPrincipalId.Value : DBNull.Value);
        command.Parameters.AddWithValue("project_id", ownerColumns.ProjectId.HasValue ? ownerColumns.ProjectId.Value : DBNull.Value);
        command.Parameters.AddWithValue("org_id", ownerColumns.OrgId.HasValue ? ownerColumns.OrgId.Value : DBNull.Value);
        command.Parameters.AddWithValue("role_id", string.IsNullOrWhiteSpace(ownerColumns.RoleId) ? DBNull.Value : ownerColumns.RoleId);
        command.Parameters.AddWithValue("agent_principal_id", ownerColumns.AgentPrincipalId.HasValue ? ownerColumns.AgentPrincipalId.Value : DBNull.Value);
    }

    private static string BuildChunkContent(MemoryProposalCommand proposal)
    {
        return $"{proposal.Subject} {proposal.Predicate} {proposal.Object}";
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record ScopeOwnerColumns(
        Guid? UserPrincipalId = null,
        Guid? ProjectId = null,
        Guid? OrgId = null,
        string? RoleId = null,
        Guid? AgentPrincipalId = null);
}
