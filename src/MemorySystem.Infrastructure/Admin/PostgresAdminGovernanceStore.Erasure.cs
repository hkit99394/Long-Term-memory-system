using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.Access;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Retention;
using MemorySystem.Infrastructure.DomainMapping;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;
public sealed partial class PostgresAdminGovernanceStore
{
    private const string ErasedTextMarker = "[erased by governance workflow]";
    private const string ErasedContentHash = "sha256:governance-erased";

    public async Task<AdminErasureExecutionResult> ExecuteErasureAsync(
        AdminErasureExecutionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateSelector(command.Selector);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var candidates = await SelectErasureCandidatesAsync(connection, transaction, command.Selector, cancellationToken);
        var targetEventIds = candidates
            .Where(candidate => !candidate.LegalHoldActive)
            .Select(candidate => candidate.EventId)
            .Distinct()
            .ToArray();

        if (targetEventIds.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);

            return new AdminErasureExecutionResult(
                candidates.Count,
                ErasedEvents: 0,
                candidates.Count(candidate => candidate.LegalHoldActive),
                RedactedFacts: 0,
                RedactedRoleLenses: 0,
                RedactedChunks: 0,
                StaleVaultExports: 0,
                ClearedReviewNotes: 0,
                RedactionRecords: 0,
                AuditEventId: null,
                DateTimeOffset.UtcNow);
        }

        var auditEventId = Guid.NewGuid();
        var auditPayload = JsonSerializer.Serialize(new
        {
            governanceAction = "erasure_executed",
            reason = command.Reason,
            targetEventCount = targetEventIds.Length
        });
        await InsertAuditEventAsync(
            connection,
            transaction,
            auditEventId,
            command.PrincipalId,
            auditPayload,
            cancellationToken);

        var factIds = await ReadRelatedIdsAsync(
            connection,
            transaction,
            """
            SELECT id
            FROM memory_facts
            WHERE source_event_id = ANY(@target_event_ids);
            """,
            targetEventIds,
            cancellationToken);

        var roleLensIds = await ReadRelatedIdsAsync(
            connection,
            transaction,
            """
            SELECT id
            FROM role_memory_lenses
            WHERE source_event_id = ANY(@target_event_ids)
                OR base_memory_fact_id = ANY(@fact_ids);
            """,
            targetEventIds,
            cancellationToken,
            factIds);

        var chunkIds = await ReadRelatedIdsAsync(
            connection,
            transaction,
            """
            SELECT id
            FROM memory_chunks
            WHERE source_event_id = ANY(@target_event_ids)
                OR (source_type = 'memory_fact' AND source_id = ANY(@fact_ids))
                OR (source_type = 'role_memory_lens' AND source_id = ANY(@role_lens_ids));
            """,
            targetEventIds,
            cancellationToken,
            factIds,
            roleLensIds);

        var vaultExportIds = await ReadRelatedIdsAsync(
            connection,
            transaction,
            """
            SELECT id
            FROM vault_exports
            WHERE source_event_id = ANY(@target_event_ids)
                OR memory_fact_id = ANY(@fact_ids);
            """,
            targetEventIds,
            cancellationToken,
            factIds);

        var redactedRoleLenses = await RedactRoleLensesAsync(connection, transaction, roleLensIds, cancellationToken);
        var redactedFacts = await RedactFactsAsync(connection, transaction, factIds, cancellationToken);
        await DeleteEmbeddingsAsync(connection, transaction, chunkIds, cancellationToken);
        var redactedChunks = await RedactChunksAsync(connection, transaction, chunkIds, cancellationToken);
        var clearedReviewNotes = await ClearReviewNotesAsync(connection, transaction, targetEventIds, factIds, cancellationToken);
        var staleVaultExports = await MarkVaultExportsStaleAsync(connection, transaction, vaultExportIds, cancellationToken);
        var redactionRecords = await InsertRedactionRecordsAsync(
            connection,
            transaction,
            auditEventId,
            command.PrincipalId,
            command.Reason,
            "event",
            targetEventIds,
            cancellationToken);
        redactionRecords += await InsertRedactionRecordsAsync(
            connection,
            transaction,
            auditEventId,
            command.PrincipalId,
            command.Reason,
            "memory_fact",
            factIds,
            cancellationToken);

        var erasedPayload = JsonSerializer.Serialize(new
        {
            erased = true,
            reason = "governance_erasure_executed",
            auditEventId
        });
        var erasedEvents = await MarkEventsErasedAsync(
            connection,
            transaction,
            targetEventIds,
            auditEventId,
            erasedPayload,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new AdminErasureExecutionResult(
            candidates.Count,
            erasedEvents,
            candidates.Count(candidate => candidate.LegalHoldActive),
            redactedFacts,
            redactedRoleLenses,
            redactedChunks,
            staleVaultExports,
            clearedReviewNotes,
            redactionRecords,
            auditEventId,
            DateTimeOffset.UtcNow);
    }

    private static async Task<IReadOnlyList<ErasureCandidate>> SelectErasureCandidatesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AdminGovernanceEventSelector selector,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(SelectErasureCandidatesSql, connection, transaction);
        AddSelectorParameters(sql, selector);

        var candidates = new List<ErasureCandidate>();
        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new ErasureCandidate(reader.GetGuid(0), reader.GetBoolean(1)));
        }

        return candidates;
    }

    private static async Task InsertAuditEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid auditEventId,
        Guid principalId,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO events (
                id,
                principal_id,
                event_type,
                content,
                content_hash,
                retention_class,
                sensitivity,
                redaction_status,
                trust_level,
                scope_type,
                scope_id
            )
            VALUES (
                @event_id,
                @principal_id,
                'memory_redacted',
                @content,
                @content_hash,
                'audit',
                'none',
                'none',
                'human_approved',
                'global',
                'global'
            );
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("event_id", auditEventId);
        sql.Parameters.AddWithValue("principal_id", principalId);
        sql.Parameters.Add("content", NpgsqlDbType.Jsonb).Value = payloadJson;
        sql.Parameters.AddWithValue("content_hash", ComputeContentHash(payloadJson));

        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid[]> ReadRelatedIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sqlText,
        IReadOnlyList<Guid> targetEventIds,
        CancellationToken cancellationToken,
        IReadOnlyList<Guid>? factIds = null,
        IReadOnlyList<Guid>? roleLensIds = null)
    {
        await using var sql = new NpgsqlCommand(sqlText, connection, transaction);
        sql.Parameters.Add("target_event_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = targetEventIds.ToArray();
        sql.Parameters.Add("fact_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = (factIds ?? []).ToArray();
        sql.Parameters.Add("role_lens_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = (roleLensIds ?? []).ToArray();

        var ids = new List<Guid>();
        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids.Distinct().ToArray();
    }

    private static async Task<int> RedactRoleLensesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> roleLensIds,
        CancellationToken cancellationToken)
    {
        if (roleLensIds.Count == 0)
        {
            return 0;
        }

        await using var sql = new NpgsqlCommand(
            """
            UPDATE role_memory_lenses
            SET interpretation = @marker,
                status = 'redacted'
            WHERE id = ANY(@role_lens_ids);
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("marker", ErasedTextMarker);
        sql.Parameters.Add("role_lens_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = roleLensIds.ToArray();

        return await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> RedactFactsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> factIds,
        CancellationToken cancellationToken)
    {
        if (factIds.Count == 0)
        {
            return 0;
        }

        await using var sql = new NpgsqlCommand(
            """
            UPDATE memory_facts
            SET subject = @marker,
                predicate = 'erased',
                object = @marker,
                confidence = 0,
                status = 'redacted'
            WHERE id = ANY(@fact_ids);
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("marker", ErasedTextMarker);
        sql.Parameters.Add("fact_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = factIds.ToArray();

        return await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> DeleteEmbeddingsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> chunkIds,
        CancellationToken cancellationToken)
    {
        if (chunkIds.Count == 0)
        {
            return 0;
        }

        await using var sql = new NpgsqlCommand(
            """
            DELETE FROM memory_embeddings
            WHERE chunk_id = ANY(@chunk_ids);
            """,
            connection,
            transaction);
        sql.Parameters.Add("chunk_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = chunkIds.ToArray();

        return await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> RedactChunksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> chunkIds,
        CancellationToken cancellationToken)
    {
        if (chunkIds.Count == 0)
        {
            return 0;
        }

        await using var sql = new NpgsqlCommand(
            """
            UPDATE memory_chunks
            SET title = NULL,
                content = @marker,
                content_hash = @content_hash,
                redacted_at = now()
            WHERE id = ANY(@chunk_ids);
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("marker", ErasedTextMarker);
        sql.Parameters.AddWithValue("content_hash", ErasedContentHash);
        sql.Parameters.Add("chunk_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = chunkIds.ToArray();

        return await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ClearReviewNotesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> targetEventIds,
        IReadOnlyList<Guid> factIds,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            UPDATE memory_reviews
            SET notes = NULL
            WHERE notes IS NOT NULL
                AND (
                    source_event_id = ANY(@target_event_ids)
                    OR memory_fact_id = ANY(@fact_ids)
                );
            """,
            connection,
            transaction);
        sql.Parameters.Add("target_event_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = targetEventIds.ToArray();
        sql.Parameters.Add("fact_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = factIds.ToArray();

        return await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> MarkVaultExportsStaleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> vaultExportIds,
        CancellationToken cancellationToken)
    {
        if (vaultExportIds.Count == 0)
        {
            return 0;
        }

        await using var sql = new NpgsqlCommand(
            """
            UPDATE vault_exports
            SET status = 'stale',
                stale_reason = 'source_erased',
                stale_at = now()
            WHERE id = ANY(@vault_export_ids);
            """,
            connection,
            transaction);
        sql.Parameters.Add("vault_export_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = vaultExportIds.ToArray();

        return await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> InsertRedactionRecordsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid auditEventId,
        Guid principalId,
        string reason,
        string targetType,
        IReadOnlyList<Guid> targetIds,
        CancellationToken cancellationToken)
    {
        if (targetIds.Count == 0)
        {
            return 0;
        }

        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO memory_redactions (
                id,
                target_type,
                target_id,
                redaction_type,
                reason,
                requested_by_principal_id,
                source_event_id
            )
            SELECT
                candidate.redaction_id,
                @target_type,
                candidate.target_id,
                'redact',
                @reason,
                @requested_by_principal_id,
                @source_event_id
            FROM unnest(@redaction_ids, @target_ids) AS candidate(redaction_id, target_id);
            """,
            connection,
            transaction);
        sql.Parameters.Add("redaction_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value =
            targetIds.Select(_ => Guid.NewGuid()).ToArray();
        sql.Parameters.Add("target_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = targetIds.ToArray();
        sql.Parameters.AddWithValue("target_type", targetType);
        sql.Parameters.AddWithValue("reason", reason);
        sql.Parameters.AddWithValue("requested_by_principal_id", principalId);
        sql.Parameters.AddWithValue("source_event_id", auditEventId);

        return await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> MarkEventsErasedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> targetEventIds,
        Guid auditEventId,
        string erasedPayload,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            UPDATE events
            SET content = @erased_payload,
                external_payload_uri = NULL,
                retention_class = 'erasure_requested',
                redaction_status = 'erased',
                redacted_at = now(),
                redaction_event_id = @redaction_event_id
            WHERE id = ANY(@target_event_ids)
                AND redaction_status <> 'erased';
            """,
            connection,
            transaction);
        sql.Parameters.Add("erased_payload", NpgsqlDbType.Jsonb).Value = erasedPayload;
        sql.Parameters.AddWithValue("redaction_event_id", auditEventId);
        sql.Parameters.Add("target_event_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = targetEventIds.ToArray();

        return await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static readonly string SelectErasureCandidatesSql = GovernanceEventSelectionCtes + """
        SELECT
            candidate.id,
            EXISTS (
                SELECT 1
                FROM governance_legal_hold_events AS link
                INNER JOIN governance_legal_holds AS hold
                    ON hold.id = link.legal_hold_id
                    AND hold.status = 'active'
                WHERE link.event_id = candidate.id
            ) AS legal_hold_active
        FROM candidate_events AS candidate
        WHERE candidate.redaction_status <> 'erased'
        ORDER BY candidate.created_at, candidate.id;
        """;

    private sealed record ErasureCandidate(
        Guid EventId,
        bool LegalHoldActive);
}
