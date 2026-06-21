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
    public async Task<AdminLegalHoldResult> CreateLegalHoldAsync(
        AdminLegalHoldCreateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateSelector(command.Selector);
        ValidateIdempotency(command.IdempotencyRecordId, command.RequestHash);
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

        var alreadyHeldEvents = candidates.Count(candidate => candidate.AlreadyHeld);

        var result = new AdminLegalHoldResult(
            holdId,
            "active",
            candidates.Count,
            candidates.Count - alreadyHeldEvents,
            alreadyHeldEvents,
            command.Reason,
            createdAt);

        await CompleteIdempotencyAsync(
            connection,
            transaction,
            command.IdempotencyRecordId,
            command.RequestHash,
            201,
            result,
            "legal_hold",
            result.HoldId,
            "The legal hold idempotency record could not be completed.",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    public async Task<AdminLegalHoldReleaseResult> ReleaseLegalHoldAsync(
        AdminLegalHoldReleaseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateIdempotency(command.IdempotencyRecordId, command.RequestHash);
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
            var inactiveResult = new AdminLegalHoldReleaseResult(
                true,
                0,
                null,
                command.HoldId,
                hold.Status,
                ReleasedEvents: 0,
                RestoredEvents: 0,
                ReleasedAt: hold.ReleasedAt);

            await CompleteLegalHoldReleaseIdempotencyAsync(
                connection,
                transaction,
                command,
                inactiveResult,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return inactiveResult;
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

        var result = new AdminLegalHoldReleaseResult(
            true,
            0,
            null,
            command.HoldId,
            "released",
            releasedEvents,
            restoredEvents,
            releasedAt);

        await CompleteLegalHoldReleaseIdempotencyAsync(
            connection,
            transaction,
            command,
            result,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    private static async Task CompleteLegalHoldReleaseIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AdminLegalHoldReleaseCommand command,
        AdminLegalHoldReleaseResult result,
        CancellationToken cancellationToken)
    {
        await CompleteIdempotencyAsync(
            connection,
            transaction,
            command.IdempotencyRecordId,
            command.RequestHash,
            200,
            new LegalHoldReleaseResponseBody(
                result.HoldId,
                result.Status,
                result.ReleasedEvents,
                result.RestoredEvents,
                result.ReleasedAt),
            "legal_hold",
            result.HoldId,
            "The legal hold release idempotency record could not be completed.",
            cancellationToken);
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
            var scope = reader.IsDBNull(6)
                ? null
                : PostgresDomainMapping.RequireScope(reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7));

            records.Add(new AdminLegalHoldRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetGuid(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                scope?.ScopeType,
                scope?.ScopeId,
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : PostgresDomainMapping.RequireRetentionClass(reader.GetString(9)),
                reader.IsDBNull(10) ? null : PostgresDomainMapping.RequireSensitivity(reader.GetString(10)),
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
                PostgresDomainMapping.RequireRetentionClass(reader.GetString(1), "originalRetentionClass"),
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

    private sealed record LegalHoldCandidate(
        Guid EventId,
        string OriginalRetentionClass,
        bool AlreadyHeld);

    private sealed record LegalHoldRow(
        string Status,
        DateTimeOffset? ReleasedAt);
}
