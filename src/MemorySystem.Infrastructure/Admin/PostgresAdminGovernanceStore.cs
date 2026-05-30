using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.Access;
using MemorySystem.Application.Admin;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminGovernanceStore(NpgsqlDataSource dataSource) : IAdminGovernanceStore
{
    private const int MaxGovernanceEventBatchSize = 500;
    private const string ErasedTextMarker = "[erased by governance workflow]";
    private const string ErasedContentHash = "sha256:governance-erased";

    public async Task<AdminLegalHoldResult> CreateLegalHoldAsync(
        AdminLegalHoldCreateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateSelector(command.Selector);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);

        var holdId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await InsertLegalHoldAsync(connection, transaction, holdId, command, cancellationToken);

        var candidates = await SelectLegalHoldCandidatesAsync(
            connection,
            transaction,
            command.Selector,
            cancellationToken);

        if (candidates.Count > 0)
        {
            await LinkLegalHoldEventsAsync(
                connection,
                transaction,
                holdId,
                candidates,
                cancellationToken);

            await MarkEventsHeldAsync(
                connection,
                transaction,
                candidates.Where(candidate => !candidate.AlreadyHeld).Select(candidate => candidate.EventId).ToArray(),
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var alreadyHeldEvents = candidates.Count(candidate => candidate.AlreadyHeld);

        return new AdminLegalHoldResult(
            holdId,
            "active",
            candidates.Count,
            candidates.Count - alreadyHeldEvents,
            alreadyHeldEvents,
            command.Reason,
            createdAt);
    }

    public async Task<AdminLegalHoldReleaseResult> ReleaseLegalHoldAsync(
        AdminLegalHoldReleaseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var hold = await ReadLegalHoldForUpdateAsync(connection, transaction, command.HoldId, cancellationToken);
        if (hold is null)
        {
            return AdminLegalHoldReleaseResult.NotFound(command.HoldId);
        }

        if (!string.Equals(hold.Status, "active", StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return new AdminLegalHoldReleaseResult(
                true,
                0,
                null,
                command.HoldId,
                hold.Status,
                ReleasedEvents: 0,
                RestoredEvents: 0,
                ReleasedAt: hold.ReleasedAt);
        }

        var eventCount = await CountLegalHoldEventsAsync(connection, transaction, command.HoldId, cancellationToken);
        var authorizedEventCount = await CountAuthorizedLegalHoldEventsAsync(
            connection,
            transaction,
            command.PrincipalId,
            command.HoldId,
            cancellationToken);

        if (authorizedEventCount != eventCount)
        {
            return AdminLegalHoldReleaseResult.Forbidden(command.HoldId);
        }

        var releasedAt = DateTimeOffset.UtcNow;
        await MarkLegalHoldReleasedAsync(connection, transaction, command, releasedAt, cancellationToken);
        var releasedEvents = await MarkLegalHoldEventsReleasedAsync(connection, transaction, command.HoldId, cancellationToken);
        var restoredEvents = await RestoreReleasedEventsAsync(connection, transaction, command.HoldId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new AdminLegalHoldReleaseResult(
            true,
            0,
            null,
            command.HoldId,
            "released",
            releasedEvents,
            restoredEvents,
            releasedAt);
    }

    public async Task<IReadOnlyList<AdminLegalHoldRecord>> ListLegalHoldsAsync(
        AdminLegalHoldListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Legal hold list limit must be between 1 and 100.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(ListLegalHoldsSql, connection);
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.Add("status", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.Status) ? DBNull.Value : query.Status;
        AddAdminAuthorizationParameters(command);

        var records = new List<AdminLegalHoldRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminLegalHoldRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetGuid(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
                reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
                reader.GetFieldValue<DateTimeOffset>(13),
                reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
                reader.GetInt32(15),
                reader.GetInt32(16),
                reader.GetInt32(17)));
        }

        return records;
    }

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

    public async Task<IReadOnlyList<AdminRetentionReportRecord>> ReadRetentionReportAsync(
        AdminRetentionReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Retention report limit must be between 1 and 200.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(RetentionReportSql, connection);
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.Add("scope_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ScopeType) ? DBNull.Value : query.ScopeType;
        command.Parameters.Add("scope_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ScopeId) ? DBNull.Value : query.ScopeId;
        command.Parameters.Add("namespace_prefix", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.NamespacePrefix) ? DBNull.Value : query.NamespacePrefix;
        AddAdminAuthorizationParameters(command);

        var records = new List<AdminRetentionReportRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminRetentionReportRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetFieldValue<DateTimeOffset>(8),
                reader.GetFieldValue<DateTimeOffset>(9)));
        }

        return records;
    }

    private static void ValidateSelector(AdminGovernanceEventSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (selector.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(selector));
        }

        if (selector.MaxEvents is < 1 or > MaxGovernanceEventBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(selector), selector.MaxEvents, $"Governance event batch size must be between 1 and {MaxGovernanceEventBatchSize}.");
        }
    }

    private static async Task InsertLegalHoldAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid holdId,
        AdminLegalHoldCreateCommand command,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO governance_legal_holds (
                id,
                status,
                reason,
                created_by_principal_id,
                scope_type,
                scope_id,
                namespace_prefix,
                retention_class,
                sensitivity,
                created_from,
                created_to
            )
            VALUES (
                @hold_id,
                'active',
                @reason,
                @created_by_principal_id,
                @scope_type,
                @scope_id,
                @namespace_prefix,
                @retention_class,
                @sensitivity,
                @created_from,
                @created_to
            );
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("hold_id", holdId);
        sql.Parameters.AddWithValue("reason", command.Reason);
        sql.Parameters.AddWithValue("created_by_principal_id", command.PrincipalId);
        AddSelectionMetadataParameters(sql, command.Selector);

        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<LegalHoldCandidate>> SelectLegalHoldCandidatesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AdminGovernanceEventSelector selector,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(SelectLegalHoldCandidatesSql, connection, transaction);
        AddSelectorParameters(sql, selector);

        var candidates = new List<LegalHoldCandidate>();
        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new LegalHoldCandidate(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetBoolean(2)));
        }

        return candidates;
    }

    private static async Task LinkLegalHoldEventsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid holdId,
        IReadOnlyList<LegalHoldCandidate> candidates,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO governance_legal_hold_events (
                legal_hold_id,
                event_id,
                original_retention_class
            )
            SELECT
                @hold_id,
                candidate.event_id,
                candidate.original_retention_class
            FROM unnest(@event_ids, @original_retention_classes) AS candidate(event_id, original_retention_class);
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("hold_id", holdId);
        sql.Parameters.Add("event_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value =
            candidates.Select(candidate => candidate.EventId).ToArray();
        sql.Parameters.Add("original_retention_classes", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            candidates.Select(candidate => candidate.OriginalRetentionClass).ToArray();

        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> MarkEventsHeldAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> eventIds,
        CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0)
        {
            return 0;
        }

        await using var sql = new NpgsqlCommand(
            """
            UPDATE events
            SET retention_class = 'legal_hold'
            WHERE id = ANY(@event_ids)
                AND retention_class <> 'legal_hold';
            """,
            connection,
            transaction);
        sql.Parameters.Add("event_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = eventIds.ToArray();

        return await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<LegalHoldRow?> ReadLegalHoldForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            SELECT status, released_at
            FROM governance_legal_holds
            WHERE id = @hold_id
            FOR UPDATE;
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("hold_id", holdId);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LegalHoldRow(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1));
    }

    private static async Task<int> CountLegalHoldEventsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM governance_legal_hold_events
            WHERE legal_hold_id = @hold_id;
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("hold_id", holdId);

        return Convert.ToInt32(await sql.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> CountAuthorizedLegalHoldEventsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid principalId,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(CountAuthorizedLegalHoldEventsSql, connection, transaction);
        sql.Parameters.AddWithValue("principal_id", principalId);
        sql.Parameters.AddWithValue("principal_id_text", principalId.ToString());
        sql.Parameters.AddWithValue("hold_id", holdId);
        AddAdminAuthorizationParameters(sql);

        return Convert.ToInt32(await sql.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task MarkLegalHoldReleasedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AdminLegalHoldReleaseCommand command,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            UPDATE governance_legal_holds
            SET status = 'released',
                release_reason = @release_reason,
                released_by_principal_id = @released_by_principal_id,
                released_at = @released_at
            WHERE id = @hold_id;
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("hold_id", command.HoldId);
        sql.Parameters.AddWithValue("release_reason", command.Reason);
        sql.Parameters.AddWithValue("released_by_principal_id", command.PrincipalId);
        sql.Parameters.AddWithValue("released_at", releasedAt);

        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> MarkLegalHoldEventsReleasedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            UPDATE governance_legal_hold_events
            SET released_at = now()
            WHERE legal_hold_id = @hold_id
                AND released_at IS NULL;
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("hold_id", holdId);

        return await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> RestoreReleasedEventsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            WITH releasable AS (
                SELECT link.event_id, link.original_retention_class
                FROM governance_legal_hold_events AS link
                WHERE link.legal_hold_id = @hold_id
                    AND NOT EXISTS (
                        SELECT 1
                        FROM governance_legal_hold_events AS other_link
                        INNER JOIN governance_legal_holds AS other_hold
                            ON other_hold.id = other_link.legal_hold_id
                        WHERE other_link.event_id = link.event_id
                            AND other_hold.status = 'active'
                    )
            )
            UPDATE events AS event
            SET retention_class = releasable.original_retention_class
            FROM releasable
            WHERE event.id = releasable.event_id
                AND event.retention_class = 'legal_hold'
                AND releasable.original_retention_class <> 'legal_hold';
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("hold_id", holdId);

        return await sql.ExecuteNonQueryAsync(cancellationToken);
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

    private static void AddSelectorParameters(NpgsqlCommand command, AdminGovernanceEventSelector selector)
    {
        command.Parameters.AddWithValue("principal_id", selector.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", selector.PrincipalId.ToString());
        command.Parameters.AddWithValue("max_events", selector.MaxEvents);
        command.Parameters.Add("event_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value =
            selector.EventIds.Count == 0 ? DBNull.Value : selector.EventIds.Distinct().ToArray();
        AddSelectionMetadataParameters(command, selector);
        AddAdminAuthorizationParameters(command);
    }

    private static void AddSelectionMetadataParameters(NpgsqlCommand command, AdminGovernanceEventSelector selector)
    {
        command.Parameters.Add("scope_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(selector.ScopeType) ? DBNull.Value : selector.ScopeType;
        command.Parameters.Add("scope_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(selector.ScopeId) ? DBNull.Value : selector.ScopeId;
        command.Parameters.Add("namespace_prefix", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(selector.NamespacePrefix) ? DBNull.Value : selector.NamespacePrefix;
        command.Parameters.Add("retention_class", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(selector.RetentionClass) ? DBNull.Value : selector.RetentionClass;
        command.Parameters.Add("sensitivity", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(selector.Sensitivity) ? DBNull.Value : selector.Sensitivity;
        command.Parameters.Add("created_from", NpgsqlDbType.TimestampTz).Value =
            selector.CreatedFrom.HasValue ? selector.CreatedFrom.Value : DBNull.Value;
        command.Parameters.Add("created_to", NpgsqlDbType.TimestampTz).Value =
            selector.CreatedTo.HasValue ? selector.CreatedTo.Value : DBNull.Value;
    }

    private static void AddAdminAuthorizationParameters(NpgsqlCommand command)
    {
        command.Parameters.Add("admin_permissions", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.GrantPermissionsFor(MemoryAccessPermissions.Admin);
        command.Parameters.Add("admin_project_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.ProjectAccessLevelsFor(MemoryAccessPermissions.Admin);
        command.Parameters.Add("admin_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.OrganizationAccessLevelsFor(MemoryAccessPermissions.Admin, allowOwner: true);
    }

    private static string ComputeContentHash(string payloadJson)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static readonly string ListLegalHoldsSql = GovernanceNamespaceCtes + """
        , visible_holds AS MATERIALIZED (
            SELECT DISTINCT hold.id
            FROM governance_legal_holds AS hold
            INNER JOIN governance_legal_hold_events AS link
                ON link.legal_hold_id = hold.id
            INNER JOIN events AS event
                ON event.id = link.event_id
            WHERE (@status IS NULL OR hold.status = @status)
                AND /*GOVERNANCE_EVENT_AUTHORIZATION*/
        )
        SELECT
            hold.id,
            hold.status,
            hold.reason,
            hold.release_reason,
            hold.created_by_principal_id,
            hold.released_by_principal_id,
            hold.scope_type,
            hold.scope_id,
            hold.namespace_prefix,
            hold.retention_class,
            hold.sensitivity,
            hold.created_from,
            hold.created_to,
            hold.created_at,
            hold.released_at,
            count(link.event_id)::int AS event_count,
            count(link.event_id) FILTER (WHERE link.released_at IS NULL)::int AS active_event_count,
            count(link.event_id) FILTER (WHERE link.released_at IS NOT NULL)::int AS released_event_count
        FROM governance_legal_holds AS hold
        INNER JOIN visible_holds
            ON visible_holds.id = hold.id
        LEFT JOIN governance_legal_hold_events AS link
            ON link.legal_hold_id = hold.id
        GROUP BY hold.id
        ORDER BY hold.created_at DESC, hold.id
        LIMIT @limit;
        """.Replace("/*GOVERNANCE_EVENT_AUTHORIZATION*/", GovernanceEventAuthorizationPredicate, StringComparison.Ordinal);

    private static readonly string CountAuthorizedLegalHoldEventsSql = GovernanceNamespaceCtes + """
        SELECT count(DISTINCT event.id)::int
        FROM governance_legal_hold_events AS link
        INNER JOIN events AS event
            ON event.id = link.event_id
        WHERE link.legal_hold_id = @hold_id
            AND /*GOVERNANCE_EVENT_AUTHORIZATION*/;
        """.Replace("/*GOVERNANCE_EVENT_AUTHORIZATION*/", GovernanceEventAuthorizationPredicate, StringComparison.Ordinal);

    private static readonly string RetentionReportSql = GovernanceNamespaceCtes + """
        SELECT
            namespace.namespace,
            event.retention_class,
            event.sensitivity,
            CASE
                WHEN event.created_at >= now() - interval '7 days' THEN '0_7_days'
                WHEN event.created_at >= now() - interval '30 days' THEN '8_30_days'
                WHEN event.created_at >= now() - interval '90 days' THEN '31_90_days'
                ELSE 'over_90_days'
            END AS age_bucket,
            count(DISTINCT event.id)::int AS event_count,
            count(DISTINCT event.id) FILTER (
                WHERE EXISTS (
                    SELECT 1
                    FROM governance_legal_hold_events AS link
                    INNER JOIN governance_legal_holds AS hold
                        ON hold.id = link.legal_hold_id
                        AND hold.status = 'active'
                    WHERE link.event_id = event.id
                )
            )::int AS legal_hold_events,
            count(DISTINCT event.id) FILTER (WHERE event.retention_class = 'erasure_requested')::int AS erasure_requested_events,
            count(DISTINCT event.id) FILTER (WHERE event.redaction_status <> 'none')::int AS redacted_events,
            min(event.created_at) AS oldest_created_at,
            max(event.created_at) AS newest_created_at
        FROM events AS event
        INNER JOIN effective_event_namespaces AS namespace
            ON namespace.event_id = event.id
        WHERE (@scope_type IS NULL OR event.scope_type = @scope_type)
            AND (@scope_id IS NULL OR event.scope_id = @scope_id)
            AND (
                @namespace_prefix IS NULL
                OR namespace.namespace = @namespace_prefix
                OR left(namespace.namespace, length(@namespace_prefix || '/')) = @namespace_prefix || '/'
            )
            AND /*GOVERNANCE_EVENT_AUTHORIZATION*/
        GROUP BY
            namespace.namespace,
            event.retention_class,
            event.sensitivity,
            age_bucket
        ORDER BY oldest_created_at, namespace.namespace, event.retention_class, event.sensitivity, age_bucket
        LIMIT @limit;
        """.Replace("/*GOVERNANCE_EVENT_AUTHORIZATION*/", GovernanceEventAuthorizationPredicate, StringComparison.Ordinal);

    private static readonly string GovernanceEventSelectionCtes = GovernanceNamespaceCtes + """
        , candidate_events AS MATERIALIZED (
            SELECT
                event.id,
                event.retention_class,
                event.redaction_status,
                event.created_at
            FROM events AS event
            WHERE (@event_ids IS NULL OR event.id = ANY(@event_ids))
                AND (@scope_type IS NULL OR event.scope_type = @scope_type)
                AND (@scope_id IS NULL OR event.scope_id = @scope_id)
                AND (@retention_class IS NULL OR event.retention_class = @retention_class)
                AND (@sensitivity IS NULL OR event.sensitivity = @sensitivity)
                AND (@created_from IS NULL OR event.created_at >= @created_from)
                AND (@created_to IS NULL OR event.created_at <= @created_to)
                AND (
                    @namespace_prefix IS NULL
                    OR EXISTS (
                        SELECT 1
                        FROM effective_event_namespaces AS namespace
                        WHERE namespace.event_id = event.id
                            AND (
                                namespace.namespace = @namespace_prefix
                                OR left(namespace.namespace, length(@namespace_prefix || '/')) = @namespace_prefix || '/'
                            )
                    )
                )
                AND /*GOVERNANCE_EVENT_AUTHORIZATION*/
            ORDER BY event.created_at, event.id
            LIMIT @max_events
            FOR UPDATE OF event SKIP LOCKED
        )
        """.Replace("/*GOVERNANCE_EVENT_AUTHORIZATION*/", GovernanceEventAuthorizationPredicate, StringComparison.Ordinal);

    private static readonly string SelectLegalHoldCandidatesSql = GovernanceEventSelectionCtes + """
        SELECT
            candidate.id,
            COALESCE(active_hold.original_retention_class, candidate.retention_class) AS original_retention_class,
            active_hold.event_id IS NOT NULL AS already_held
        FROM candidate_events AS candidate
        LEFT JOIN LATERAL (
            SELECT link.event_id, link.original_retention_class
            FROM governance_legal_hold_events AS link
            INNER JOIN governance_legal_holds AS hold
                ON hold.id = link.legal_hold_id
                AND hold.status = 'active'
            WHERE link.event_id = candidate.id
            ORDER BY link.held_at, link.legal_hold_id
            LIMIT 1
        ) AS active_hold ON TRUE
        WHERE candidate.redaction_status = 'none'
            AND candidate.retention_class <> 'erasure_requested'
        ORDER BY candidate.created_at, candidate.id;
        """;

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

    private const string GovernanceNamespaceCtes = """
        WITH referenced_event_namespaces AS MATERIALIZED (
            SELECT source_event_id AS event_id, namespace
            FROM memory_facts

            UNION

            SELECT source_event_id AS event_id, namespace
            FROM memory_chunks

            UNION

            SELECT
                source_event_id AS event_id,
                CASE scope_type
                    WHEN 'global' THEN '/role/' || role_id || '/shared'
                    WHEN 'org' THEN '/org/' || org_id::text || '/role/' || role_id || '/lens'
                    WHEN 'project' THEN '/project/' || project_id::text || '/role/' || role_id || '/lens'
                    ELSE '/role/' || role_id || '/lens'
                END AS namespace
            FROM role_memory_lenses
        ),
        effective_event_namespaces AS MATERIALIZED (
            SELECT event_id, namespace
            FROM referenced_event_namespaces

            UNION ALL

            SELECT
                event.id AS event_id,
                '/' || event.scope_type || '/' || event.scope_id || '/events' AS namespace
            FROM events AS event
            WHERE NOT EXISTS (
                SELECT 1
                FROM referenced_event_namespaces AS referenced
                WHERE referenced.event_id = event.id
            )
        )
        """;

    private const string GovernanceEventAuthorizationPredicate = """
        (
            (
                event.scope_type = 'global'
                OR (
                    event.scope_type = 'user'
                    AND (
                        event.scope_principal_id = @principal_id
                        OR event.scope_id = @principal_id_text
                    )
                )
                OR (
                    event.scope_type = 'agent'
                    AND (
                        event.agent_principal_id = @principal_id
                        OR event.scope_id = @principal_id_text
                    )
                )
                OR (
                    event.scope_type = 'role'
                    AND event.scope_role_id IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM role_assignments AS assignment
                        WHERE assignment.principal_id = @principal_id
                            AND assignment.role_id = event.scope_role_id
                            AND assignment.scope_type = 'global'
                    )
                )
                OR (
                    event.scope_type = 'org'
                    AND event.scope_org_id IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM organization_memberships AS membership
                        WHERE membership.principal_id = @principal_id
                            AND membership.org_id = event.scope_org_id
                            AND membership.access_level = ANY(@admin_org_access_levels)
                    )
                )
                OR (
                    event.scope_type = 'project'
                    AND event.scope_project_id IS NOT NULL
                    AND (
                        EXISTS (
                            SELECT 1
                            FROM project_memberships AS membership
                            INNER JOIN projects AS project_membership
                                ON project_membership.id = membership.project_id
                                AND project_membership.status = 'active'
                            WHERE membership.principal_id = @principal_id
                                AND membership.project_id = event.scope_project_id
                                AND membership.access_level = ANY(@admin_project_access_levels)
                        )
                        OR (
                            event.scope_org_id IS NOT NULL
                            AND EXISTS (
                                SELECT 1
                                FROM organization_memberships AS membership
                                WHERE membership.principal_id = @principal_id
                                    AND membership.org_id = event.scope_org_id
                                    AND membership.access_level = ANY(@admin_org_access_levels)
                            )
                        )
                    )
                )
            )
            AND EXISTS (
                SELECT 1
                FROM effective_event_namespaces AS namespace
                WHERE namespace.event_id = event.id
                    AND (
                        EXISTS (
                            SELECT 1
                            FROM memory_access_grants AS grant_record
                            WHERE grant_record.principal_id = @principal_id
                                AND grant_record.permission = ANY(@admin_permissions)
                                AND (
                                    namespace.namespace = grant_record.namespace_prefix
                                    OR left(namespace.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                                )
                        )
                        OR EXISTS (
                            SELECT 1
                            FROM memory_access_grants AS grant_record
                            WHERE grant_record.role_id IS NOT NULL
                                AND grant_record.permission = ANY(@admin_permissions)
                                AND (
                                    namespace.namespace = grant_record.namespace_prefix
                                    OR left(namespace.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                                )
                                AND EXISTS (
                                    SELECT 1
                                    FROM role_assignments AS assignment
                                    WHERE assignment.principal_id = @principal_id
                                        AND assignment.role_id = grant_record.role_id
                                        AND (
                                            assignment.scope_type = 'global'
                                            OR (
                                                event.scope_org_id IS NOT NULL
                                                AND assignment.scope_type = 'org'
                                                AND assignment.scope_id = event.scope_org_id
                                            )
                                            OR (
                                                event.scope_project_id IS NOT NULL
                                                AND assignment.scope_type = 'project'
                                                AND assignment.scope_id = event.scope_project_id
                                            )
                                        )
                                )
                        )
                    )
            )
        )
        """;

    private sealed record LegalHoldCandidate(
        Guid EventId,
        string OriginalRetentionClass,
        bool AlreadyHeld);

    private sealed record LegalHoldRow(
        string Status,
        DateTimeOffset? ReleasedAt);

    private sealed record ErasureCandidate(
        Guid EventId,
        bool LegalHoldActive);
}
