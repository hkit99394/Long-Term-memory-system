using System.Text.Json;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Idempotency;
using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Infrastructure.RoleMemoryLenses;
using MemorySystem.Infrastructure.Scopes;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryProposals;

public sealed class PostgresMemoryProposalWriteStore(NpgsqlDataSource dataSource) : IMemoryProposalWriteStore
{
    private const string ActiveMemorySubjectPredicateIndexName = "ux_memory_facts_active_subject_predicate";
    private const string ActiveRoleMemoryLensDedupeIndexName = "ux_role_memory_lenses_active_dedupe";
    private const string MemoryFactInsertSavepoint = "memory_fact_insert";
    private const string RoleMemoryLensInsertSavepoint = "role_memory_lens_insert";
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

        if (proposal.CandidateKind == MemoryCandidateClassifications.RoleLens)
        {
            return await StoreRoleMemoryLensAsync(
                proposedByPrincipalId,
                proposal,
                idempotencyRecordId,
                requestHash,
                cancellationToken);
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
        catch (PostgresException exception) when (IsActiveMemoryConflictViolation(exception))
        {
            await ExecuteTransactionCommandAsync(connection, transaction, $"ROLLBACK TO SAVEPOINT {MemoryFactInsertSavepoint};", cancellationToken);
            await ExecuteTransactionCommandAsync(connection, transaction, $"RELEASE SAVEPOINT {MemoryFactInsertSavepoint};", cancellationToken);

            var existingMemory = await FindActiveMemoryFactBySubjectPredicateAsync(
                connection,
                transaction,
                proposal,
                cancellationToken);
            var duplicateDecision = string.Equals(
                NormalizeComparableText(existingMemory.Object),
                NormalizeComparableText(proposal.Object),
                StringComparison.Ordinal)
                    ? new MemoryProposalDecision(
                        MemoryProposalDecisions.Stored,
                        "The proposal matched existing durable memory.",
                        existingMemory.Id,
                        proposal.SourceEventId,
                        proposal.CandidateKind,
                        proposal.Confidence)
                    : new MemoryProposalDecision(
                        MemoryProposalDecisions.ReviewRequired,
                        "A similar active memory already exists and should be reviewed before storing another version.",
                        MemoryId: null,
                        proposal.SourceEventId,
                        proposal.CandidateKind,
                        proposal.Confidence);

            await CompleteIdempotencyAsync(
                connection,
                transaction,
                idempotencyRecordId,
                requestHash,
                duplicateDecision,
                duplicateDecision.Decision == MemoryProposalDecisions.Stored ? "memory_fact" : null,
                duplicateDecision.MemoryId,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return duplicateDecision;
        }

        await MemoryIndexWriteOperations.InsertMemoryChunkAsync(
            connection,
            transaction,
            chunkId,
            MemoryIndexOutboxJobContract.AggregateType,
            memoryId,
            proposal.Namespace,
            proposal.ScopeType,
            proposal.ScopeId,
            proposal.Subject,
            chunkContent,
            proposal.TrustLevel,
            proposal.SourceEventId.Value,
            cancellationToken);
        await MemoryIndexWriteOperations.InsertOutboxJobAsync(
            connection,
            transaction,
            outboxJobId,
            MemoryIndexOutboxJobContract.AggregateType,
            memoryId,
            chunkId,
            proposal.SourceEventId.Value,
            cancellationToken);
        var decision = new MemoryProposalDecision(
            MemoryProposalDecisions.Stored,
            "The proposal was stored as durable memory.",
            memoryId,
            proposal.SourceEventId,
            proposal.CandidateKind,
            proposal.Confidence);
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

    private async Task<MemoryProposalDecision> StoreRoleMemoryLensAsync(
        Guid proposedByPrincipalId,
        MemoryProposalCommand proposal,
        Guid idempotencyRecordId,
        string requestHash,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(proposal.RoleId))
        {
            throw new InvalidOperationException("Stored role-lens proposals require role id.");
        }

        if (!proposal.BaseMemoryFactId.HasValue || proposal.BaseMemoryFactId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Stored role-lens proposals require base memory fact id.");
        }

        var roleMemoryLensId = Guid.NewGuid();
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
        var lensScope = RoleMemoryLensStorageRules.ResolveLensScope(
            proposal.ScopeType,
            proposal.ScopeId,
            ownerColumns.OrgId,
            ownerColumns.ProjectId);
        var chunkScope = RoleMemoryLensStorageRules.ResolveChunkScope(
            lensScope,
            proposal.RoleId,
            proposal.Namespace);

        await ExecuteTransactionCommandAsync(connection, transaction, $"SAVEPOINT {RoleMemoryLensInsertSavepoint};", cancellationToken);

        try
        {
            await InsertRoleMemoryLensAsync(
                connection,
                transaction,
                roleMemoryLensId,
                proposedByPrincipalId,
                proposal,
                lensScope,
                cancellationToken);
            await ExecuteTransactionCommandAsync(connection, transaction, $"RELEASE SAVEPOINT {RoleMemoryLensInsertSavepoint};", cancellationToken);
        }
        catch (PostgresException exception) when (IsActiveRoleMemoryLensConflictViolation(exception))
        {
            await ExecuteTransactionCommandAsync(connection, transaction, $"ROLLBACK TO SAVEPOINT {RoleMemoryLensInsertSavepoint};", cancellationToken);
            await ExecuteTransactionCommandAsync(connection, transaction, $"RELEASE SAVEPOINT {RoleMemoryLensInsertSavepoint};", cancellationToken);

            var existingRoleMemoryLensId = await FindActiveRoleMemoryLensAsync(
                connection,
                transaction,
                proposal,
                lensScope,
                cancellationToken);
            var duplicateDecision = new MemoryProposalDecision(
                MemoryProposalDecisions.Stored,
                "The proposal matched existing durable role memory lens.",
                existingRoleMemoryLensId,
                proposal.SourceEventId,
                proposal.CandidateKind,
                proposal.Confidence);

            await CompleteIdempotencyAsync(
                connection,
                transaction,
                idempotencyRecordId,
                requestHash,
                duplicateDecision,
                "role_memory_lens",
                existingRoleMemoryLensId,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return duplicateDecision;
        }

        await MemoryIndexWriteOperations.InsertMemoryChunkAsync(
            connection,
            transaction,
            chunkId,
            MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType,
            roleMemoryLensId,
            chunkScope.Namespace,
            chunkScope.ScopeType,
            chunkScope.ScopeId,
            proposal.Subject,
            chunkContent,
            proposal.TrustLevel,
            proposal.SourceEventId!.Value,
            cancellationToken);
        await MemoryIndexWriteOperations.InsertOutboxJobAsync(
            connection,
            transaction,
            outboxJobId,
            MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType,
            roleMemoryLensId,
            chunkId,
            proposal.SourceEventId.Value,
            cancellationToken);

        var decision = new MemoryProposalDecision(
            MemoryProposalDecisions.Stored,
            "The proposal was stored as a role memory lens.",
            roleMemoryLensId,
            proposal.SourceEventId,
            proposal.CandidateKind,
            proposal.Confidence);
        await CompleteIdempotencyAsync(
            connection,
            transaction,
            idempotencyRecordId,
            requestHash,
            decision,
            "role_memory_lens",
            roleMemoryLensId,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return decision;
    }

    private static bool IsActiveMemoryConflictViolation(PostgresException exception)
    {
        return exception.SqlState == PostgresErrorCodes.UniqueViolation
            && (
                string.Equals(exception.ConstraintName, ActiveMemorySubjectPredicateIndexName, StringComparison.Ordinal)
                || exception.MessageText.Contains(ActiveMemorySubjectPredicateIndexName, StringComparison.Ordinal)
            );
    }

    private static bool IsActiveRoleMemoryLensConflictViolation(PostgresException exception)
    {
        return exception.SqlState == PostgresErrorCodes.UniqueViolation
            && (
                string.Equals(exception.ConstraintName, ActiveRoleMemoryLensDedupeIndexName, StringComparison.Ordinal)
                || exception.MessageText.Contains(ActiveRoleMemoryLensDedupeIndexName, StringComparison.Ordinal)
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
        string? resourceType,
        Guid? resourceId,
        CancellationToken cancellationToken)
    {
        await PostgresApiIdempotencyCompleter.CompleteAsync(
            connection,
            transaction,
            idempotencyRecordId,
            requestHash,
            200,
            JsonSerializer.Serialize(decision, JsonOptions),
            "application/json; charset=utf-8",
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

    private static async Task<ExistingActiveMemoryFact> FindActiveMemoryFactBySubjectPredicateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MemoryProposalCommand proposal,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, object
            FROM memory_facts
            WHERE status = 'active'
                AND scope_type = @scope_type
                AND scope_id = @scope_id
                AND memory_type = @memory_type
                AND lower(btrim(subject)) = lower(btrim(@subject))
                AND lower(btrim(predicate)) = lower(btrim(@predicate))
            ORDER BY created_at, id
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddProposalParameters(command, proposal);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? new ExistingActiveMemoryFact(reader.GetGuid(0), reader.GetString(1))
            : throw new InvalidOperationException("Active memory fact was not found after subject/predicate conflict.");
    }

    private static async Task<Guid> FindActiveRoleMemoryLensAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MemoryProposalCommand proposal,
        RoleMemoryLensStorageScope lensScope,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id
            FROM role_memory_lenses
            WHERE status = 'active'
                AND role_id = @role_id
                AND scope_type = @scope_type
                AND scope_id = @scope_id
                AND base_memory_fact_id = @base_memory_fact_id
                AND lower(btrim(interpretation)) = lower(btrim(@interpretation))
            ORDER BY created_at, id
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("role_id", proposal.RoleId!);
        RoleMemoryLensStorageRules.AddScopeParameters(command, lensScope);
        command.Parameters.AddWithValue("base_memory_fact_id", proposal.BaseMemoryFactId!.Value);
        command.Parameters.AddWithValue("interpretation", NormalizeStorageText(proposal.Object));

        return await command.ExecuteScalarAsync(cancellationToken) is Guid existingRoleMemoryLensId
            ? existingRoleMemoryLensId
            : throw new InvalidOperationException("Active role memory lens was not found after duplicate conflict.");
    }

    private static async Task InsertRoleMemoryLensAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid roleMemoryLensId,
        Guid proposedByPrincipalId,
        MemoryProposalCommand proposal,
        RoleMemoryLensStorageScope lensScope,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO role_memory_lenses (
                id,
                role_id,
                scope_type,
                scope_id,
                org_id,
                project_id,
                base_memory_fact_id,
                interpretation,
                confidence,
                status,
                source_event_id,
                proposed_by_principal_id
            )
            VALUES (
                @id,
                @role_id,
                @scope_type,
                @scope_id,
                @org_id,
                @project_id,
                @base_memory_fact_id,
                @interpretation,
                @confidence,
                'active',
                @source_event_id,
                @proposed_by_principal_id
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", roleMemoryLensId);
        command.Parameters.AddWithValue("role_id", proposal.RoleId!);
        RoleMemoryLensStorageRules.AddScopeParameters(command, lensScope);
        RoleMemoryLensStorageRules.AddOwnerParameters(command, lensScope);
        command.Parameters.AddWithValue("base_memory_fact_id", proposal.BaseMemoryFactId!.Value);
        command.Parameters.AddWithValue("interpretation", NormalizeStorageText(proposal.Object));
        command.Parameters.AddWithValue("confidence", proposal.Confidence!.Value);
        command.Parameters.AddWithValue("source_event_id", proposal.SourceEventId!.Value);
        command.Parameters.AddWithValue("proposed_by_principal_id", proposedByPrincipalId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddProposalParameters(NpgsqlCommand command, MemoryProposalCommand proposal)
    {
        command.Parameters.AddWithValue("scope_type", proposal.ScopeType);
        command.Parameters.AddWithValue("scope_id", proposal.ScopeId);
        command.Parameters.AddWithValue("namespace", proposal.Namespace);
        command.Parameters.AddWithValue("memory_type", proposal.MemoryType);
        command.Parameters.AddWithValue("visibility", proposal.Visibility);
        command.Parameters.AddWithValue("subject", NormalizeStorageText(proposal.Subject));
        command.Parameters.AddWithValue("predicate", NormalizeStorageText(proposal.Predicate));
        command.Parameters.AddWithValue("object", NormalizeStorageText(proposal.Object));
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
        return MemoryIndexWriteOperations.BuildFactChunkContent(
            NormalizeStorageText(proposal.Subject),
            NormalizeStorageText(proposal.Predicate),
            NormalizeStorageText(proposal.Object));
    }

    private static string NormalizeComparableText(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeStorageText(string value)
    {
        return value.Trim();
    }

    private sealed record ScopeOwnerColumns(
        Guid? UserPrincipalId = null,
        Guid? ProjectId = null,
        Guid? OrgId = null,
        string? RoleId = null,
        Guid? AgentPrincipalId = null);

    private sealed record ExistingActiveMemoryFact(Guid Id, string Object);

}
