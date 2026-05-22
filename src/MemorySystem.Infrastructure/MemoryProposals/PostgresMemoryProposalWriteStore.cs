using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Idempotency;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryProposals;

public sealed class PostgresMemoryProposalWriteStore(NpgsqlDataSource dataSource) : IMemoryProposalWriteStore
{
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
        var decision = new MemoryProposalDecision(
            MemoryProposalDecisions.Stored,
            "The proposal was stored as durable memory.",
            memoryId,
            proposal.SourceEventId);
        var responseBody = JsonSerializer.Serialize(decision, JsonOptions);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var ownerColumns = await ResolveOwnerColumnsAsync(
            connection,
            transaction,
            proposal.ScopeType,
            proposal.ScopeId,
            cancellationToken);

        await InsertMemoryFactAsync(
            connection,
            transaction,
            memoryId,
            proposedByPrincipalId,
            proposal,
            ownerColumns,
            cancellationToken);
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
        await PostgresApiIdempotencyCompleter.CompleteAsync(
            connection,
            transaction,
            idempotencyRecordId,
            requestHash,
            200,
            responseBody,
            "memory_fact",
            memoryId,
            "The proposal idempotency record could not be completed.",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return decision;
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
            "project" => new ScopeOwnerColumns(
                OrgId: await ResolveProjectOrgIdAsync(connection, transaction, Guid.Parse(scopeId), cancellationToken),
                ProjectId: Guid.Parse(scopeId)),
            _ => throw new InvalidOperationException($"Unsupported memory proposal scope type '{scopeType}'.")
        };
    }

    private static async Task<Guid> ResolveProjectOrgIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT org_id FROM projects WHERE id = @project_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("project_id", projectId);

        var value = await command.ExecuteScalarAsync(cancellationToken);

        return value is Guid orgId
            ? orgId
            : throw new InvalidOperationException($"Project scope {projectId} does not reference an existing project.");
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
        command.Parameters.AddWithValue("source_event_id", proposal.SourceEventId!.Value);
        command.Parameters.AddWithValue("proposed_by_principal_id", proposedByPrincipalId);

        await command.ExecuteNonQueryAsync(cancellationToken);
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
                'memory_fact',
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
                'memory.index',
                'memory_fact',
                @aggregate_id,
                @idempotency_key,
                @payload,
                'pending'
            );
            """;

        var payload = JsonSerializer.Serialize(new
        {
            memoryFactId = memoryId,
            chunkId,
            sourceEventId
        }, JsonOptions);

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", outboxJobId);
        command.Parameters.AddWithValue("aggregate_id", memoryId);
        command.Parameters.AddWithValue("idempotency_key", $"memory.index:{memoryId:N}");
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = payload;

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
