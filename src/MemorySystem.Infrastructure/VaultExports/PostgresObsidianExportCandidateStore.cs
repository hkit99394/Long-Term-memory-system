using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.VaultExports;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.VaultExports;

public sealed class PostgresObsidianExportCandidateStore(NpgsqlDataSource dataSource) : IObsidianExportCandidateStore
{
    public async Task<IReadOnlyList<ObsidianExportCandidate>> ListCandidatesAsync(
        ObsidianExportCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Obsidian export candidate limit must be between 1 and 500.");
        }

        if (query.Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Offset, "Obsidian export candidate offset must not be negative.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                fact.id,
                fact.scope_type,
                fact.scope_id,
                fact.namespace,
                fact.user_principal_id,
                fact.project_id,
                fact.org_id,
                fact.role_id,
                fact.agent_principal_id,
                fact.memory_type,
                fact.visibility,
                fact.subject,
                fact.predicate,
                fact.object,
                fact.confidence,
                fact.trust_level,
                fact.status,
                fact.source_event_id,
                fact.proposed_by_principal_id,
                fact.created_at,
                fact.updated_at
            FROM memory_facts AS fact
            WHERE fact.status = 'active'
                AND fact.memory_type IN ('decision', 'summary')
                AND (@scope_type IS NULL OR (fact.scope_type = @scope_type AND fact.scope_id = @scope_id))
                AND (
                    fact.trust_level IN ('system_trusted', 'human_approved')
                    OR EXISTS (
                        SELECT 1
                        FROM memory_reviews AS review
                        WHERE review.memory_fact_id = fact.id
                            AND review.review_status = 'approved'
                    )
                )
            ORDER BY fact.scope_type, fact.scope_id, fact.memory_type, lower(fact.subject), fact.id
            LIMIT @limit
            OFFSET @offset;
            """,
            connection);
        command.Parameters.Add("scope_type", NpgsqlTypes.NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ScopeType) ? DBNull.Value : query.ScopeType;
        command.Parameters.Add("scope_id", NpgsqlTypes.NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ScopeId) ? DBNull.Value : query.ScopeId;
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("offset", query.Offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var candidates = new List<ObsidianExportCandidate>();

        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(ReadCandidate(reader));
        }

        return candidates;
    }

    public async Task<IReadOnlyList<ObsidianStaleExportCandidate>> ListStaleCandidatesAsync(
        ObsidianExportCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Obsidian stale export candidate limit must be between 1 and 500.");
        }

        if (query.Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Offset, "Obsidian stale export candidate offset must not be negative.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                fact.id,
                fact.scope_type,
                fact.scope_id,
                fact.namespace,
                fact.user_principal_id,
                fact.project_id,
                fact.org_id,
                fact.role_id,
                fact.agent_principal_id,
                fact.memory_type,
                fact.visibility,
                fact.subject,
                fact.predicate,
                fact.object,
                fact.confidence,
                fact.trust_level,
                fact.status,
                fact.source_event_id,
                fact.proposed_by_principal_id,
                fact.created_at,
                fact.updated_at,
                export.export_path,
                export.exported_at
            FROM vault_exports AS export
            INNER JOIN memory_facts AS fact
                ON fact.id = export.memory_fact_id
            WHERE export.export_type = 'obsidian_markdown'
                AND export.status = 'current'
                AND fact.status IN ('superseded', 'contradicted', 'expired', 'deleted', 'redacted')
                AND (@scope_type IS NULL OR (fact.scope_type = @scope_type AND fact.scope_id = @scope_id))
            ORDER BY export.updated_at, export.id
            LIMIT @limit
            OFFSET @offset;
            """,
            connection);
        command.Parameters.Add("scope_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ScopeType) ? DBNull.Value : query.ScopeType;
        command.Parameters.Add("scope_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ScopeId) ? DBNull.Value : query.ScopeId;
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("offset", query.Offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var candidates = new List<ObsidianStaleExportCandidate>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var memoryFact = ReadMemoryFact(reader);
            candidates.Add(new ObsidianStaleExportCandidate(
                memoryFact,
                reader.GetString(21),
                $"memory_{memoryFact.Status}",
                reader.GetFieldValue<DateTimeOffset>(22)));
        }

        return candidates;
    }

    public async Task<IReadOnlyList<ObsidianArchiveExportCandidate>> ListArchiveCandidatesAsync(
        ObsidianExportCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Obsidian archive export candidate limit must be between 1 and 500.");
        }

        if (query.Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Offset, "Obsidian archive export candidate offset must not be negative.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                fact.id,
                fact.scope_type,
                fact.scope_id,
                fact.namespace,
                fact.user_principal_id,
                fact.project_id,
                fact.org_id,
                fact.role_id,
                fact.agent_principal_id,
                fact.memory_type,
                fact.visibility,
                fact.subject,
                fact.predicate,
                fact.object,
                fact.confidence,
                fact.trust_level,
                fact.status,
                fact.source_event_id,
                fact.proposed_by_principal_id,
                fact.created_at,
                fact.updated_at
            FROM memory_facts AS fact
            WHERE fact.status IN ('superseded', 'contradicted', 'expired')
                AND (@scope_type IS NULL OR (fact.scope_type = @scope_type AND fact.scope_id = @scope_id))
            ORDER BY fact.updated_at DESC, fact.created_at DESC, fact.id
            LIMIT @limit
            OFFSET @offset;
            """,
            connection);
        command.Parameters.Add("scope_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ScopeType) ? DBNull.Value : query.ScopeType;
        command.Parameters.Add("scope_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ScopeId) ? DBNull.Value : query.ScopeId;
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("offset", query.Offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var candidates = new List<ObsidianArchiveExportCandidate>();

        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new ObsidianArchiveExportCandidate(
                ReadMemoryFact(reader),
                reader.GetFieldValue<DateTimeOffset>(19),
                reader.GetFieldValue<DateTimeOffset>(20)));
        }

        return candidates;
    }

    public async Task RecordExportedAsync(
        IReadOnlyList<ObsidianExportDocument> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var document in documents)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO vault_exports (
                    id,
                    memory_fact_id,
                    export_type,
                    export_path,
                    source_event_id,
                    status,
                    stale_reason,
                    stale_at
                )
                VALUES (
                    @id,
                    @memory_fact_id,
                    'obsidian_markdown',
                    @export_path,
                    @source_event_id,
                    'current',
                    NULL,
                    NULL
                )
                ON CONFLICT (export_type, memory_fact_id)
                DO UPDATE SET
                    export_path = EXCLUDED.export_path,
                    source_event_id = EXCLUDED.source_event_id,
                    status = 'current',
                    stale_reason = NULL,
                    exported_at = now(),
                    stale_at = NULL;
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("memory_fact_id", document.MemoryFactId);
            command.Parameters.AddWithValue("export_path", document.Path);
            command.Parameters.AddWithValue("source_event_id", document.SourceEventId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordStaleAsync(
        IReadOnlyList<ObsidianStaleExportDocument> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var document in documents)
        {
            await using var command = new NpgsqlCommand(
                """
                UPDATE vault_exports
                SET status = 'stale',
                    stale_reason = @stale_reason,
                    stale_at = COALESCE(stale_at, now())
                WHERE export_type = 'obsidian_markdown'
                    AND memory_fact_id = @memory_fact_id;
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("memory_fact_id", document.MemoryFactId);
            command.Parameters.AddWithValue("stale_reason", document.Reason);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordArchiveAsync(
        IReadOnlyList<ObsidianArchiveExportDocument> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var document in documents)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO vault_exports (
                    id,
                    memory_fact_id,
                    export_type,
                    export_path,
                    source_event_id,
                    status,
                    stale_reason,
                    stale_at
                )
                VALUES (
                    @id,
                    @memory_fact_id,
                    'obsidian_archive',
                    @export_path,
                    @source_event_id,
                    'current',
                    NULL,
                    NULL
                )
                ON CONFLICT (export_type, memory_fact_id)
                DO UPDATE SET
                    export_path = EXCLUDED.export_path,
                    source_event_id = EXCLUDED.source_event_id,
                    status = 'current',
                    stale_reason = NULL,
                    exported_at = now(),
                    stale_at = NULL;
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("memory_fact_id", document.MemoryFactId);
            command.Parameters.AddWithValue("export_path", document.Path);
            command.Parameters.AddWithValue("source_event_id", document.SourceEventId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static ObsidianExportCandidate ReadCandidate(NpgsqlDataReader reader)
    {
        return new ObsidianExportCandidate(
            ReadMemoryFact(reader),
            reader.GetFieldValue<DateTimeOffset>(19),
            reader.GetFieldValue<DateTimeOffset>(20));
    }

    private static MemoryFactRecord ReadMemoryFact(NpgsqlDataReader reader)
    {
        return new MemoryFactRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetString(12),
            reader.GetString(13),
            reader.GetDecimal(14),
            reader.GetString(15),
            reader.GetString(16),
            reader.GetGuid(17),
            reader.IsDBNull(18) ? null : reader.GetGuid(18));
    }
}
