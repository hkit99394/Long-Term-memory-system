using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryReviews;
using MemorySystem.Infrastructure.Idempotency;
using MemorySystem.Infrastructure.Outbox;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryReviews;

public sealed class PostgresMemoryReviewActionStore(
    NpgsqlDataSource dataSource,
    IMemoryReviewActionIdempotencyResponseSerializer responseSerializer) : IMemoryReviewActionStore
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
            cancellationToken);

        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MemoryReviewActionStoreResult.NotApplied();
        }

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
                var editSourceEvent = await FindSourceEventEvidenceAsync(
                    connection,
                    transaction,
                    command.SourceEventId,
                    cancellationToken);
                await UpdateMemoryFactContentAsync(
                    connection,
                    transaction,
                    current.MemoryFact.Id,
                    command.Subject!,
                    command.Predicate!,
                    command.Object!,
                    command.SourceEventId,
                    editSourceEvent.TrustLevel,
                    editSourceEvent.ProposedByPrincipalId,
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
                        TrustLevel = editSourceEvent.TrustLevel,
                        Status = MemoryFactStatuses.Active,
                        SourceEventId = command.SourceEventId,
                        ProposedByPrincipalId = editSourceEvent.ProposedByPrincipalId
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
                await MemoryIndexWriteOperations.RedactMemoryFactChunksAsync(
                    connection,
                    transaction,
                    current.MemoryFact.Id,
                    cancellationToken);
                await InsertMemoryRedactionAsync(
                    connection,
                    transaction,
                    current.MemoryFact.Id,
                    command.ReviewerId,
                    command.SourceEventId,
                    cancellationToken);
                break;

            case MemoryReviewActions.Supersede:
                await InsertReplacementMemoryFactAsync(
                    connection,
                    transaction,
                    replacementMemoryFactId!.Value,
                    current.MemoryFact,
                    command.Subject!,
                    command.Predicate!,
                    command.Object!,
                    command.SourceEventId,
                    cancellationToken);
                await MarkMemoryFactSupersededAsync(
                    connection,
                    transaction,
                    current.MemoryFact.Id,
                    replacementMemoryFactId.Value,
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

        await CompleteIdempotencyAsync(
            connection,
            transaction,
            command,
            updatedReview,
            replacementMemoryFactId,
            responseSerializer,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new MemoryReviewActionStoreResult(updatedReview, replacementMemoryFactId);
    }

    private static async Task CompleteIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MemoryReviewActionStoreCommand command,
        MemoryReviewRecord review,
        Guid? replacementMemoryFactId,
        IMemoryReviewActionIdempotencyResponseSerializer responseSerializer,
        CancellationToken cancellationToken)
    {
        var response = responseSerializer.Serialize(command.Action, review, replacementMemoryFactId);

        await PostgresApiIdempotencyCompleter.CompleteAsync(
            connection,
            transaction,
            command.IdempotencyRecordId,
            command.RequestHash,
            response.StatusCode,
            response.BodyJson,
            response.ContentType,
            response.ResourceType,
            response.ResourceId,
            "The review action idempotency record could not be completed.",
            cancellationToken);
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
            ? PostgresMemoryReviewRows.ReadReview(reader)
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

    private static async Task MarkMemoryFactSupersededAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid memoryFactId,
        Guid supersededByMemoryFactId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE memory_facts
            SET status = @status,
                superseded_by = @superseded_by
            WHERE id = @memory_fact_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("status", MemoryFactStatuses.Superseded);
        command.Parameters.AddWithValue("superseded_by", supersededByMemoryFactId);

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
        string trustLevel,
        Guid proposedByPrincipalId,
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
                trust_level = @trust_level,
                proposed_by_principal_id = @proposed_by_principal_id,
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
        command.Parameters.AddWithValue("trust_level", trustLevel);
        command.Parameters.AddWithValue("proposed_by_principal_id", proposedByPrincipalId);
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
        CancellationToken cancellationToken)
    {
        var sourceEvent = await FindSourceEventEvidenceAsync(connection, transaction, sourceEventId, cancellationToken);

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
        command.Parameters.AddWithValue("trust_level", sourceEvent.TrustLevel);
        command.Parameters.AddWithValue("status", MemoryFactStatuses.Active);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("proposed_by_principal_id", sourceEvent.ProposedByPrincipalId);

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
                TrustLevel = sourceEvent.TrustLevel,
                Status = MemoryFactStatuses.Active,
                SourceEventId = sourceEventId,
                ProposedByPrincipalId = sourceEvent.ProposedByPrincipalId
            },
            sourceEventId,
            cancellationToken);
    }

    private static async Task InsertMemoryRedactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid memoryFactId,
        Guid requestedByPrincipalId,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
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
            VALUES (
                @id,
                'memory_fact',
                @target_id,
                'delete',
                @reason,
                @requested_by_principal_id,
                @source_event_id
            );
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("target_id", memoryFactId);
        command.Parameters.AddWithValue("reason", "Memory review delete action.");
        command.Parameters.AddWithValue("requested_by_principal_id", requestedByPrincipalId);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync(cancellationToken);
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
        var chunkContent = MemoryIndexWriteOperations.BuildFactChunkContent(
            memoryFact.Subject,
            memoryFact.Predicate,
            memoryFact.Object);
        var chunkId = await FindMemoryChunkIdAsync(connection, transaction, memoryFact.Id, cancellationToken);

        if (chunkId.HasValue)
        {
            await MemoryIndexWriteOperations.UpdateMemoryChunkAsync(
                connection,
                transaction,
                chunkId.Value,
                memoryFact.Namespace,
                memoryFact.ScopeType,
                memoryFact.ScopeId,
                memoryFact.Subject,
                chunkContent,
                memoryFact.TrustLevel,
                sourceEventId,
                cancellationToken);
            await MemoryIndexWriteOperations.UpsertOutboxJobAsync(
                connection,
                transaction,
                MemoryIndexOutboxJobContract.AggregateType,
                memoryFact.Id,
                chunkId.Value,
                sourceEventId,
                cancellationToken);
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
        await MemoryIndexWriteOperations.UpsertOutboxJobAsync(
            connection,
            transaction,
            MemoryIndexOutboxJobContract.AggregateType,
            memoryFact.Id,
            chunkId.Value,
            sourceEventId,
            cancellationToken);
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

    private static async Task<SourceEventEvidence> FindSourceEventEvidenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                trust_level,
                COALESCE(
                    principal_id,
                    scope_principal_id,
                    agent_principal_id,
                    '00000000-0000-4000-8000-000000000007'::uuid)
            FROM events
            WHERE id = @source_event_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? new SourceEventEvidence(reader.GetString(0), reader.GetGuid(1))
            : throw new InvalidOperationException($"Source event {sourceEventId} could not be read.");
    }

    private static string ReviewStatusFor(string action)
    {
        return action == MemoryReviewActions.Reject
            ? MemoryReviewStatuses.Rejected
            : MemoryReviewStatuses.Approved;
    }

    private sealed record SourceEventEvidence(
        string TrustLevel,
        Guid ProposedByPrincipalId);

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
