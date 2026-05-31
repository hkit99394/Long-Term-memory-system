using MemorySystem.Application.MemoryEvaluations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryEvaluations;

public sealed class PostgresMemoryRetrievalFeedbackStore(NpgsqlDataSource dataSource) : IMemoryRetrievalFeedbackStore
{
    public async Task<MemoryRetrievalFeedbackRecord> StoreAsync(
        MemoryRetrievalFeedbackCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command);

        var feedbackId = Guid.NewGuid();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO memory_retrieval_feedback (
                id,
                principal_id,
                retrieval_mode,
                query_hash,
                packet_id,
                item_id,
                target_scope_type,
                target_scope_id,
                role_id,
                source_type,
                source_id,
                feedback_type
            )
            VALUES (
                @id,
                @principal_id,
                @retrieval_mode,
                @query_hash,
                @packet_id,
                @item_id,
                @target_scope_type,
                @target_scope_id,
                @role_id,
                @source_type,
                @source_id,
                @feedback_type
            )
            RETURNING created_at;
            """,
            connection);
        sql.Parameters.AddWithValue("id", feedbackId);
        sql.Parameters.AddWithValue("principal_id", command.PrincipalId);
        sql.Parameters.AddWithValue("retrieval_mode", command.RetrievalMode);
        sql.Parameters.AddWithValue("query_hash", command.QueryHash);
        sql.Parameters.Add("packet_id", NpgsqlDbType.Uuid).Value =
            command.PacketId.HasValue ? command.PacketId.Value : DBNull.Value;
        sql.Parameters.Add("item_id", NpgsqlDbType.Uuid).Value =
            command.ItemId.HasValue ? command.ItemId.Value : DBNull.Value;
        sql.Parameters.Add("target_scope_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(command.TargetScopeType) ? DBNull.Value : command.TargetScopeType;
        sql.Parameters.Add("target_scope_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(command.TargetScopeId) ? DBNull.Value : command.TargetScopeId;
        sql.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(command.RoleId) ? DBNull.Value : command.RoleId;
        sql.Parameters.Add("source_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(command.SourceType) ? DBNull.Value : command.SourceType;
        sql.Parameters.Add("source_id", NpgsqlDbType.Uuid).Value =
            command.SourceId.HasValue ? command.SourceId.Value : DBNull.Value;
        sql.Parameters.AddWithValue("feedback_type", command.FeedbackType);

        var createdAt = await sql.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Retrieval feedback insert did not return a creation timestamp.");

        return new MemoryRetrievalFeedbackRecord(
            feedbackId,
            command.PrincipalId,
            command.RetrievalMode,
            command.QueryHash,
            command.PacketId,
            command.ItemId,
            command.TargetScopeType,
            command.TargetScopeId,
            command.RoleId,
            command.SourceType,
            command.SourceId,
            command.FeedbackType,
            ToDateTimeOffset(createdAt));
    }

    private static void Validate(MemoryRetrievalFeedbackCommand command)
    {
        if (command.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(command));
        }

        if (!string.Equals(command.RetrievalMode, "context_packet", StringComparison.Ordinal))
        {
            throw new ArgumentException("Retrieval mode is not supported.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.QueryHash) || !command.QueryHash.StartsWith("sha256:", StringComparison.Ordinal))
        {
            throw new ArgumentException("Query hash is required.", nameof(command));
        }

        if (command.PacketId == Guid.Empty)
        {
            throw new ArgumentException("Packet id is invalid.", nameof(command));
        }

        if (command.ItemId == Guid.Empty)
        {
            throw new ArgumentException("Item id is invalid.", nameof(command));
        }

        if (command.ItemId.HasValue && !command.PacketId.HasValue)
        {
            throw new ArgumentException("Item feedback requires packet id.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.TargetScopeType) != string.IsNullOrWhiteSpace(command.TargetScopeId))
        {
            throw new ArgumentException("Target scope type and id must be provided together.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.SourceType) != !command.SourceId.HasValue)
        {
            throw new ArgumentException("Source type and id must be provided together.", nameof(command));
        }

        if (!string.IsNullOrWhiteSpace(command.SourceType)
            && command.SourceType is not "memory_fact" and not "role_memory_lens")
        {
            throw new ArgumentException("Source type is not supported.", nameof(command));
        }

        if (!MemoryRetrievalFeedbackTypes.TryNormalize(command.FeedbackType, out var feedbackType, out _)
            || !string.Equals(feedbackType, command.FeedbackType, StringComparison.Ordinal))
        {
            throw new ArgumentException("Feedback type is not supported.", nameof(command));
        }

        if (MemoryRetrievalFeedbackTypes.RequiresSource(command.FeedbackType)
            && !command.SourceId.HasValue
            && !command.ItemId.HasValue)
        {
            throw new ArgumentException("Useful, stale, and noisy feedback must identify a retrieved source or item.", nameof(command));
        }
    }

    private static DateTimeOffset ToDateTimeOffset(object value)
    {
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("Unexpected timestamp value returned from PostgreSQL.")
        };
    }
}
