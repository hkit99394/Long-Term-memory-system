using System.Security.Cryptography;
using System.Text;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryReviews;
using MemorySystem.Infrastructure.Outbox;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryReviews;

public sealed class PostgresMemoryReviewActionStore(NpgsqlDataSource dataSource) : IMemoryReviewActionStore
{
    public async Task<MemoryReviewRecord?> FindPendingAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await FindReviewAsync(
            connection,
            transaction: null,
            reviewId,
            pendingOnly: true,
            forUpdate: false,
            cancellationToken);
    }

    public async Task<MemoryReviewActionStoreResult> ApplyAsync(
        MemoryReviewActionStoreCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Review);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var current = await FindReviewAsync(
            connection,
            transaction,
            command.Review.Id,
            pendingOnly: true,
            forUpdate: true,
            cancellationToken)
            ?? throw new InvalidOperationException($"Pending review {command.Review.Id} could not be locked.");

        var replacementMemoryFactId = command.Action == MemoryReviewActions.Supersede
            ? Guid.NewGuid()
            : (Guid?)null;

        switch (command.Action)
        {
            case MemoryReviewActions.Approve:
                await UpdateMemoryFactStatusAsync(
                    connection,
                    transaction,
                    current.MemoryFact.Id,
                    MemoryFactStatuses.Active,
                    cancellationToken);
                await EnsureMemoryChunkAsync(connection, transaction, current.MemoryFact, current.MemoryFact.SourceEventId, cancellationToken);
                break;

            case MemoryReviewActions.Reject:
                await UpdateMemoryFactStatusAsync(
                    connection,
                    transaction,
                    current.MemoryFact.Id,
                    MemoryFactStatuses.Deleted,
                    cancellationToken);
                break;

            case MemoryReviewActions.Edit:
                await UpdateMemoryFactContentAsync(
                    connection,
                    transaction,
                    current.MemoryFact.Id,
                    command.Subject!,
                    command.Predicate!,
                    command.Object!,
                    command.SourceEventId,
                    MemoryFactStatuses.Active,
                    cancellationToken);
                await EnsureMemoryChunkAsync(
                    connection,
                    transaction,
                    current.MemoryFact with
                    {
                        Subject = command.Subject!,
                        Predicate = command.Predicate!,
                        Object = command.Object!,
                        Status = MemoryFactStatuses.Active,
                        SourceEventId = command.SourceEventId
                    },
                    command.SourceEventId,
                    cancellationToken);
                break;

            case MemoryReviewActions.Expire:
                await UpdateMemoryFactStatusAsync(
                    connection,
                    transaction,
                    current.MemoryFact.Id,
                    MemoryFactStatuses.Expired,
                    cancellationToken);
                break;

            case MemoryReviewActions.Delete:
                await UpdateMemoryFactStatusAsync(
                    connection,
                    transaction,
                    current.MemoryFact.Id,
                    MemoryFactStatuses.Deleted,
                    cancellationToken);
                break;

            case MemoryReviewActions.Supersede:
                await UpdateMemoryFactStatusAsync(
                    connection,
                    transaction,
                    current.MemoryFact.Id,
                    MemoryFactStatuses.Superseded,
                    cancellationToken);
                await InsertReplacementMemoryFactAsync(
                    connection,
                    transaction,
                    replacementMemoryFactId!.Value,
                    current.MemoryFact,
                    command.Subject!,
                    command.Predicate!,
                    command.Object!,
                    command.SourceEventId,
                    command.ReviewerId,
                    cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unsupported review action '{command.Action}'.");
        }

        await CompleteReviewAsync(
            connection,
            transaction,
            current.Id,
            ReviewStatusFor(command.Action),
            command.ReviewerId,
            command.SourceEventId,
            command.Notes,
            cancellationToken);

        var updatedReview = await FindReviewAsync(
            connection,
            transaction,
            current.Id,
            pendingOnly: false,
            forUpdate: false,
            cancellationToken)
            ?? throw new InvalidOperationException($"Review {current.Id} could not be read after update.");

        await transaction.CommitAsync(cancellationToken);

        return new MemoryReviewActionStoreResult(updatedReview, replacementMemoryFactId);
    }

    private static async Task<MemoryReviewRecord?> FindReviewAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid reviewId,
        bool pendingOnly,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var sql = FindReviewSql
            + (pendingOnly ? " AND review.review_status = @pending_status" : string.Empty)
            + (forUpdate ? " FOR UPDATE OF review, fact" : string.Empty)
            + ";";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("review_id", reviewId);

        if (pendingOnly)
        {
            command.Parameters.AddWithValue("pending_status", MemoryReviewStatuses.Pending);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? ReadReview(reader)
            : null;
    }

    private static async Task UpdateMemoryFactStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid memoryFactId,
        string status,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE memory_facts
            SET status = @status
            WHERE id = @memory_fact_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateMemoryFactContentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid memoryFactId,
        string subject,
        string predicate,
        string objectValue,
        Guid sourceEventId,
        string status,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE memory_facts
            SET subject = @subject,
                predicate = @predicate,
                object = @object,
                source_event_id = @source_event_id,
                status = @status
            WHERE id = @memory_fact_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("predicate", predicate);
        command.Parameters.AddWithValue("object", objectValue);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertReplacementMemoryFactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid replacementMemoryFactId,
        MemoryFactRecord original,
        string subject,
        string predicate,
        string objectValue,
        Guid sourceEventId,
        Guid proposedByPrincipalId,
        CancellationToken cancellationToken)
    {
        var trustLevel = await FindSourceEventTrustLevelAsync(connection, transaction, sourceEventId, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
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
                @status,
                @source_event_id,
                @proposed_by_principal_id
            );
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", replacementMemoryFactId);
        command.Parameters.AddWithValue("scope_type", original.ScopeType);
        command.Parameters.AddWithValue("scope_id", original.ScopeId);
        command.Parameters.AddWithValue("namespace", original.Namespace);
        command.Parameters.Add("user_principal_id", NpgsqlDbType.Uuid).Value =
            original.UserPrincipalId.HasValue ? original.UserPrincipalId.Value : DBNull.Value;
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            original.ProjectId.HasValue ? original.ProjectId.Value : DBNull.Value;
        command.Parameters.Add("org_id", NpgsqlDbType.Uuid).Value =
            original.OrgId.HasValue ? original.OrgId.Value : DBNull.Value;
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(original.RoleId) ? DBNull.Value : original.RoleId;
        command.Parameters.Add("agent_principal_id", NpgsqlDbType.Uuid).Value =
            original.AgentPrincipalId.HasValue ? original.AgentPrincipalId.Value : DBNull.Value;
        command.Parameters.AddWithValue("memory_type", original.MemoryType);
        command.Parameters.AddWithValue("visibility", original.Visibility);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("predicate", predicate);
        command.Parameters.AddWithValue("object", objectValue);
        command.Parameters.AddWithValue("confidence", original.Confidence);
        command.Parameters.AddWithValue("trust_level", trustLevel);
        command.Parameters.AddWithValue("status", MemoryFactStatuses.Active);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("proposed_by_principal_id", proposedByPrincipalId);

        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureMemoryChunkAsync(
            connection,
            transaction,
            original with
            {
                Id = replacementMemoryFactId,
                Subject = subject,
                Predicate = predicate,
                Object = objectValue,
                TrustLevel = trustLevel,
                Status = MemoryFactStatuses.Active,
                SourceEventId = sourceEventId,
                ProposedByPrincipalId = proposedByPrincipalId
            },
            sourceEventId,
            cancellationToken);
    }

    private static async Task CompleteReviewAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid reviewId,
        string reviewStatus,
        Guid reviewerId,
        Guid sourceEventId,
        string? notes,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE memory_reviews
            SET review_status = @review_status,
                reviewer_id = @reviewer_id,
                notes = @notes,
                source_event_id = @source_event_id
            WHERE id = @review_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("review_id", reviewId);
        command.Parameters.AddWithValue("review_status", reviewStatus);
        command.Parameters.AddWithValue("reviewer_id", reviewerId);
        command.Parameters.Add("notes", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes;
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureMemoryChunkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MemoryFactRecord memoryFact,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        var chunkContent = BuildChunkContent(memoryFact.Subject, memoryFact.Predicate, memoryFact.Object);
        var chunkId = await FindMemoryChunkIdAsync(connection, transaction, memoryFact.Id, cancellationToken);

        if (chunkId.HasValue)
        {
            await using var update = new NpgsqlCommand(
                """
                UPDATE memory_chunks
                SET namespace = @namespace,
                    scope_type = @scope_type,
                    scope_id = @scope_id,
                    title = @title,
                    content = @content,
                    content_hash = @content_hash,
                    trust_level = @trust_level,
                    source_event_id = @source_event_id,
                    redacted_at = NULL
                WHERE id = @chunk_id;
                """,
                connection,
                transaction);
            update.Parameters.AddWithValue("chunk_id", chunkId.Value);
            AddChunkParameters(update, memoryFact, chunkContent, sourceEventId);
            await update.ExecuteNonQueryAsync(cancellationToken);
            await UpsertOutboxJobAsync(connection, transaction, memoryFact.Id, chunkId.Value, sourceEventId, cancellationToken);
            return;
        }

        chunkId = Guid.NewGuid();
        await MemoryIndexWriteOperations.InsertMemoryChunkAsync(
            connection,
            transaction,
            chunkId.Value,
            MemoryIndexOutboxJobContract.AggregateType,
            memoryFact.Id,
            memoryFact.Namespace,
            memoryFact.ScopeType,
            memoryFact.ScopeId,
            memoryFact.Subject,
            chunkContent,
            memoryFact.TrustLevel,
            sourceEventId,
            cancellationToken);
        await UpsertOutboxJobAsync(connection, transaction, memoryFact.Id, chunkId.Value, sourceEventId, cancellationToken);
    }

    private static async Task<Guid?> FindMemoryChunkIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid memoryFactId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id
            FROM memory_chunks
            WHERE source_type = 'memory_fact'
                AND source_id = @memory_fact_id
            ORDER BY created_at, id
            LIMIT 1;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is Guid chunkId ? chunkId : null;
    }

    private static async Task UpsertOutboxJobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid memoryFactId,
        Guid chunkId,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
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
            )
            ON CONFLICT (idempotency_key)
            DO UPDATE SET
                payload = EXCLUDED.payload,
                status = 'pending',
                attempts = 0,
                available_at = now(),
                locked_until = NULL,
                locked_by = NULL,
                last_error = NULL;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("job_type", MemoryIndexOutboxJobContract.JobType);
        command.Parameters.AddWithValue("aggregate_type", MemoryIndexOutboxJobContract.AggregateType);
        command.Parameters.AddWithValue("aggregate_id", memoryFactId);
        command.Parameters.AddWithValue(
            "idempotency_key",
            MemoryIndexOutboxJobContract.CreateIdempotencyKey(memoryFactId));
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value =
            MemoryIndexOutboxJobContract.SerializePayload(memoryFactId, chunkId, sourceEventId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddChunkParameters(
        NpgsqlCommand command,
        MemoryFactRecord memoryFact,
        string chunkContent,
        Guid sourceEventId)
    {
        command.Parameters.AddWithValue("namespace", memoryFact.Namespace);
        command.Parameters.AddWithValue("scope_type", memoryFact.ScopeType);
        command.Parameters.AddWithValue("scope_id", memoryFact.ScopeId);
        command.Parameters.AddWithValue("title", memoryFact.Subject);
        command.Parameters.AddWithValue("content", chunkContent);
        command.Parameters.AddWithValue("content_hash", ComputeSha256(chunkContent));
        command.Parameters.AddWithValue("trust_level", memoryFact.TrustLevel);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
    }

    private static async Task<string> FindSourceEventTrustLevelAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT trust_level
            FROM events
            WHERE id = @source_event_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is string trustLevel
            ? trustLevel
            : throw new InvalidOperationException($"Source event {sourceEventId} could not be read.");
    }

    private static string ReviewStatusFor(string action)
    {
        return action == MemoryReviewActions.Reject
            ? MemoryReviewStatuses.Rejected
            : MemoryReviewStatuses.Approved;
    }

    private static string BuildChunkContent(string subject, string predicate, string objectValue)
    {
        return $"{subject} {predicate} {objectValue}";
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static MemoryReviewRecord ReadReview(NpgsqlDataReader reader)
    {
        var memoryFact = new MemoryFactRecord(
            reader.GetGuid(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetGuid(12),
            reader.IsDBNull(13) ? null : reader.GetGuid(13),
            reader.IsDBNull(14) ? null : reader.GetGuid(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetGuid(16),
            reader.GetString(17),
            reader.GetString(18),
            reader.GetString(19),
            reader.GetString(20),
            reader.GetString(21),
            reader.GetDecimal(22),
            reader.GetString(23),
            reader.GetString(24),
            reader.GetGuid(25),
            reader.IsDBNull(26) ? null : reader.GetGuid(26));

        return new MemoryReviewRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetGuid(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            memoryFact);
    }

    private const string FindReviewSql = """
        SELECT
            review.id,
            review.memory_fact_id,
            review.review_status,
            review.reviewer_id,
            review.notes,
            review.source_event_id,
            review.created_at,
            review.updated_at,
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
            fact.proposed_by_principal_id
        FROM memory_reviews AS review
        INNER JOIN memory_facts AS fact
            ON fact.id = review.memory_fact_id
        WHERE review.id = @review_id
        """;
}
