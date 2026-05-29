using MemorySystem.Infrastructure.Outbox;
using Npgsql;
using NpgsqlTypes;

internal static partial class Scenario0001Seeder
{
    private static async Task UpsertMemoryFactsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await UpsertMemoryFactAsync(
            connection,
            transaction,
            Scenario0001.UserPreferenceMemoryFactId,
            "user",
            Scenario0001.PrincipalId.ToString(),
            $"/user/{Scenario0001.PrincipalId}/preferences",
            userPrincipalId: Scenario0001.PrincipalId,
            projectId: null,
            orgId: null,
            roleId: null,
            memoryType: "preference",
            visibility: "private",
            subject: "technical planning format",
            predicate: "prefers",
            objectValue: "concise decision logs with short rationale and explicit tradeoffs",
            confidence: 0.900m,
            trustLevel: "user_scoped",
            sourceEventId: Scenario0001.UserPreferenceEventId,
            cancellationToken);
        await UpsertMemoryFactAsync(
            connection,
            transaction,
            Scenario0001.ProjectDecisionMemoryFactId,
            "project",
            Scenario0001.ProjectAId.ToString(),
            $"/project/{Scenario0001.ProjectAId}/decisions",
            userPrincipalId: null,
            projectId: Scenario0001.ProjectAId,
            orgId: Scenario0001.OrgId,
            roleId: null,
            memoryType: "decision",
            visibility: "project_shared",
            subject: "M1-M3 data access",
            predicate: "uses",
            objectValue: "SQL-first migrations plus raw Npgsql",
            confidence: 0.950m,
            trustLevel: "human_approved",
            sourceEventId: Scenario0001.ProjectDecisionEventId,
            cancellationToken);
        await UpsertMemoryFactAsync(
            connection,
            transaction,
            Scenario0001.SharedCtoPrincipleMemoryFactId,
            "org",
            Scenario0001.OrgId.ToString(),
            $"/org/{Scenario0001.OrgId}/policies",
            userPrincipalId: null,
            projectId: null,
            orgId: Scenario0001.OrgId,
            roleId: null,
            memoryType: "role_principle",
            visibility: "org_shared",
            subject: "CTO context packet",
            predicate: "should_foreground",
            objectValue: "architecture risk, operational reversibility, delivery sequencing, and security boundaries",
            confidence: 0.920m,
            trustLevel: "human_approved",
            sourceEventId: Scenario0001.SharedCtoPrincipleEventId,
            cancellationToken);
        await UpsertMemoryFactAsync(
            connection,
            transaction,
            Scenario0001.ProjectBDecisionMemoryFactId,
            "project",
            Scenario0001.ProjectBId.ToString(),
            $"/project/{Scenario0001.ProjectBId}/decisions",
            userPrincipalId: null,
            projectId: Scenario0001.ProjectBId,
            orgId: Scenario0001.OrgId,
            roleId: null,
            memoryType: "decision",
            visibility: "project_shared",
            subject: "funding strategy",
            predicate: "uses",
            objectValue: "confidential runway model",
            confidence: 0.990m,
            trustLevel: "human_approved",
            sourceEventId: Scenario0001.ProjectBDecisionEventId,
            cancellationToken);
    }

    private static async Task UpsertMemoryFactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string scopeType,
        string scopeId,
        string namespaceValue,
        Guid? userPrincipalId,
        Guid? projectId,
        Guid? orgId,
        string? roleId,
        string memoryType,
        string visibility,
        string subject,
        string predicate,
        string objectValue,
        decimal confidence,
        string trustLevel,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
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
            )
            ON CONFLICT (id)
            DO UPDATE SET
                scope_type = EXCLUDED.scope_type,
                scope_id = EXCLUDED.scope_id,
                namespace = EXCLUDED.namespace,
                user_principal_id = EXCLUDED.user_principal_id,
                project_id = EXCLUDED.project_id,
                org_id = EXCLUDED.org_id,
                role_id = EXCLUDED.role_id,
                memory_type = EXCLUDED.memory_type,
                visibility = EXCLUDED.visibility,
                subject = EXCLUDED.subject,
                predicate = EXCLUDED.predicate,
                object = EXCLUDED.object,
                confidence = EXCLUDED.confidence,
                trust_level = EXCLUDED.trust_level,
                status = EXCLUDED.status,
                source_event_id = EXCLUDED.source_event_id,
                proposed_by_principal_id = EXCLUDED.proposed_by_principal_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.AddWithValue("namespace", namespaceValue);
        command.Parameters.Add("user_principal_id", NpgsqlDbType.Uuid).Value =
            userPrincipalId.HasValue ? userPrincipalId.Value : DBNull.Value;
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            projectId.HasValue ? projectId.Value : DBNull.Value;
        command.Parameters.Add("org_id", NpgsqlDbType.Uuid).Value =
            orgId.HasValue ? orgId.Value : DBNull.Value;
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(roleId) ? DBNull.Value : roleId;
        command.Parameters.AddWithValue("memory_type", memoryType);
        command.Parameters.AddWithValue("visibility", visibility);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("predicate", predicate);
        command.Parameters.AddWithValue("object", objectValue);
        command.Parameters.AddWithValue("confidence", confidence);
        command.Parameters.AddWithValue("trust_level", trustLevel);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("proposed_by_principal_id", Scenario0001.PrincipalId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertRoleMemoryLensesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await UpsertRoleMemoryLensAsync(
            connection,
            transaction,
            Scenario0001.SharedCtoPrincipleRoleMemoryLensId,
            scopeType: "org",
            scopeId: Scenario0001.OrgId.ToString(),
            orgId: Scenario0001.OrgId,
            projectId: null,
            baseMemoryFactId: Scenario0001.SharedCtoPrincipleMemoryFactId,
            interpretation:
                "A CTO context packet should foreground architecture risk, operational reversibility, delivery sequencing, and security boundaries.",
            confidence: 0.920m,
            sourceEventId: Scenario0001.SharedCtoPrincipleEventId,
            cancellationToken);
        await UpsertRoleMemoryLensAsync(
            connection,
            transaction,
            Scenario0001.ProjectCtoLensRoleMemoryLensId,
            scopeType: "project",
            scopeId: Scenario0001.ProjectAId.ToString(),
            orgId: Scenario0001.OrgId,
            projectId: Scenario0001.ProjectAId,
            baseMemoryFactId: Scenario0001.ProjectDecisionMemoryFactId,
            interpretation:
                "For the CTO view, the SQL-first Npgsql decision should be treated as a risk-reduction move: it keeps authorization predicates visible while the schema and event provenance model are still stabilizing.",
            confidence: 0.930m,
            sourceEventId: Scenario0001.ProjectCtoLensEventId,
            cancellationToken);
    }

    private static async Task UpsertRoleMemoryLensAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string scopeType,
        string scopeId,
        Guid? orgId,
        Guid? projectId,
        Guid baseMemoryFactId,
        string interpretation,
        decimal confidence,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
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
                'cto',
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
            )
            ON CONFLICT (id)
            DO UPDATE SET
                role_id = EXCLUDED.role_id,
                scope_type = EXCLUDED.scope_type,
                scope_id = EXCLUDED.scope_id,
                org_id = EXCLUDED.org_id,
                project_id = EXCLUDED.project_id,
                base_memory_fact_id = EXCLUDED.base_memory_fact_id,
                interpretation = EXCLUDED.interpretation,
                confidence = EXCLUDED.confidence,
                status = EXCLUDED.status,
                source_event_id = EXCLUDED.source_event_id,
                proposed_by_principal_id = EXCLUDED.proposed_by_principal_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.Add("org_id", NpgsqlDbType.Uuid).Value =
            orgId.HasValue ? orgId.Value : DBNull.Value;
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            projectId.HasValue ? projectId.Value : DBNull.Value;
        command.Parameters.AddWithValue("base_memory_fact_id", baseMemoryFactId);
        command.Parameters.AddWithValue("interpretation", interpretation);
        command.Parameters.AddWithValue("confidence", confidence);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);
        command.Parameters.AddWithValue("proposed_by_principal_id", Scenario0001.PrincipalId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertMemoryChunksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await UpsertMemoryChunkAsync(
            connection,
            transaction,
            Scenario0001.UserPreferenceChunkId,
            MemoryIndexOutboxJobContract.AggregateType,
            Scenario0001.UserPreferenceMemoryFactId,
            $"/user/{Scenario0001.PrincipalId}/preferences",
            "user",
            Scenario0001.PrincipalId.ToString(),
            "technical planning format",
            "technical planning format prefers concise decision logs with short rationale and explicit tradeoffs",
            "user_scoped",
            Scenario0001.UserPreferenceEventId,
            cancellationToken);
        await UpsertMemoryChunkAsync(
            connection,
            transaction,
            Scenario0001.ProjectDecisionChunkId,
            MemoryIndexOutboxJobContract.AggregateType,
            Scenario0001.ProjectDecisionMemoryFactId,
            $"/project/{Scenario0001.ProjectAId}/decisions",
            "project",
            Scenario0001.ProjectAId.ToString(),
            "M1-M3 data access",
            "M1-M3 data access uses SQL-first migrations plus raw Npgsql",
            "human_approved",
            Scenario0001.ProjectDecisionEventId,
            cancellationToken);
        await UpsertMemoryChunkAsync(
            connection,
            transaction,
            Scenario0001.SharedCtoPrincipleFactChunkId,
            MemoryIndexOutboxJobContract.AggregateType,
            Scenario0001.SharedCtoPrincipleMemoryFactId,
            $"/org/{Scenario0001.OrgId}/policies",
            "org",
            Scenario0001.OrgId.ToString(),
            "CTO context packet",
            "CTO context packet should_foreground architecture risk, operational reversibility, delivery sequencing, and security boundaries",
            "human_approved",
            Scenario0001.SharedCtoPrincipleEventId,
            cancellationToken);
        await UpsertMemoryChunkAsync(
            connection,
            transaction,
            Scenario0001.SharedCtoPrincipleLensChunkId,
            MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType,
            Scenario0001.SharedCtoPrincipleRoleMemoryLensId,
            $"/org/{Scenario0001.OrgId}/role/cto/lens",
            "org",
            Scenario0001.OrgId.ToString(),
            "cto",
            "A CTO context packet should foreground architecture risk, operational reversibility, delivery sequencing, and security boundaries.",
            "human_approved",
            Scenario0001.SharedCtoPrincipleEventId,
            cancellationToken);
        await UpsertMemoryChunkAsync(
            connection,
            transaction,
            Scenario0001.ProjectCtoLensChunkId,
            MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType,
            Scenario0001.ProjectCtoLensRoleMemoryLensId,
            $"/project/{Scenario0001.ProjectAId}/role/cto/lens",
            "project",
            Scenario0001.ProjectAId.ToString(),
            "cto",
            "For the CTO view, the SQL-first Npgsql decision should be treated as a risk-reduction move: it keeps authorization predicates visible while the schema and event provenance model are still stabilizing.",
            "human_approved",
            Scenario0001.ProjectCtoLensEventId,
            cancellationToken);
        await UpsertMemoryChunkAsync(
            connection,
            transaction,
            Scenario0001.ProjectBDecisionChunkId,
            MemoryIndexOutboxJobContract.AggregateType,
            Scenario0001.ProjectBDecisionMemoryFactId,
            $"/project/{Scenario0001.ProjectBId}/decisions",
            "project",
            Scenario0001.ProjectBId.ToString(),
            "funding strategy",
            "funding strategy uses confidential runway model",
            "human_approved",
            Scenario0001.ProjectBDecisionEventId,
            cancellationToken);
    }

    private static async Task UpsertMemoryChunkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string sourceType,
        Guid sourceId,
        string namespaceValue,
        string scopeType,
        string scopeId,
        string title,
        string content,
        string trustLevel,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
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
                source_event_id,
                redacted_at
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
                @source_event_id,
                NULL
            )
            ON CONFLICT (id)
            DO UPDATE SET
                source_type = EXCLUDED.source_type,
                source_id = EXCLUDED.source_id,
                namespace = EXCLUDED.namespace,
                scope_type = EXCLUDED.scope_type,
                scope_id = EXCLUDED.scope_id,
                title = EXCLUDED.title,
                content = EXCLUDED.content,
                content_hash = EXCLUDED.content_hash,
                trust_level = EXCLUDED.trust_level,
                source_event_id = EXCLUDED.source_event_id,
                redacted_at = NULL;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("source_type", sourceType);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("namespace", namespaceValue);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("content_hash", ComputeSha256(content));
        command.Parameters.AddWithValue("trust_level", trustLevel);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
