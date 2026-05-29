using Npgsql;
using NpgsqlTypes;

internal static partial class Scenario0001Seeder
{
    private static async Task UpsertEventsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await UpsertEventAsync(
            connection,
            transaction,
            Scenario0001.UserPreferenceEventId,
            "user",
            Scenario0001.PrincipalId.ToString(),
            scopeOrgId: null,
            scopeProjectId: null,
            scopePrincipalId: Scenario0001.PrincipalId,
            scopeRoleId: null,
            trustLevel: "user_scoped",
            contentJson: Scenario0001.UserPreferenceEventContent,
            cancellationToken);
        await UpsertEventAsync(
            connection,
            transaction,
            Scenario0001.ProjectDecisionEventId,
            "project",
            Scenario0001.ProjectAId.ToString(),
            scopeOrgId: Scenario0001.OrgId,
            scopeProjectId: Scenario0001.ProjectAId,
            scopePrincipalId: null,
            scopeRoleId: null,
            trustLevel: "human_approved",
            contentJson: Scenario0001.ProjectDecisionEventContent,
            cancellationToken);
        await UpsertEventAsync(
            connection,
            transaction,
            Scenario0001.SharedCtoPrincipleEventId,
            "org",
            Scenario0001.OrgId.ToString(),
            scopeOrgId: Scenario0001.OrgId,
            scopeProjectId: null,
            scopePrincipalId: null,
            scopeRoleId: null,
            trustLevel: "human_approved",
            contentJson: Scenario0001.SharedCtoPrincipleEventContent,
            cancellationToken);
        await UpsertEventAsync(
            connection,
            transaction,
            Scenario0001.ProjectCtoLensEventId,
            "project",
            Scenario0001.ProjectAId.ToString(),
            scopeOrgId: Scenario0001.OrgId,
            scopeProjectId: Scenario0001.ProjectAId,
            scopePrincipalId: null,
            scopeRoleId: null,
            trustLevel: "human_approved",
            contentJson: Scenario0001.ProjectCtoLensEventContent,
            cancellationToken);
        await UpsertEventAsync(
            connection,
            transaction,
            Scenario0001.ProjectBDecisionEventId,
            "project",
            Scenario0001.ProjectBId.ToString(),
            scopeOrgId: Scenario0001.OrgId,
            scopeProjectId: Scenario0001.ProjectBId,
            scopePrincipalId: null,
            scopeRoleId: null,
            trustLevel: "human_approved",
            contentJson: Scenario0001.ProjectBDecisionEventContent,
            cancellationToken);
    }

    private static async Task UpsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid eventId,
        string scopeType,
        string scopeId,
        Guid? scopeOrgId,
        Guid? scopeProjectId,
        Guid? scopePrincipalId,
        string? scopeRoleId,
        string trustLevel,
        string contentJson,
        CancellationToken cancellationToken,
        string redactionStatus = "none")
    {
        await using var command = new NpgsqlCommand(
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
                scope_id,
                scope_org_id,
                scope_project_id,
                scope_principal_id,
                scope_role_id
            )
            VALUES (
                @event_id,
                @principal_id,
                'user_message',
                @content,
                @content_hash,
                'standard',
                'none',
                @redaction_status,
                @trust_level,
                @scope_type,
                @scope_id,
                @scope_org_id,
                @scope_project_id,
                @scope_principal_id,
                @scope_role_id
            )
            ON CONFLICT (id)
            DO UPDATE SET
                principal_id = EXCLUDED.principal_id,
                event_type = EXCLUDED.event_type,
                content = EXCLUDED.content,
                content_hash = EXCLUDED.content_hash,
                retention_class = EXCLUDED.retention_class,
                sensitivity = EXCLUDED.sensitivity,
                redaction_status = EXCLUDED.redaction_status,
                trust_level = EXCLUDED.trust_level,
                scope_type = EXCLUDED.scope_type,
                scope_id = EXCLUDED.scope_id,
                scope_org_id = EXCLUDED.scope_org_id,
                scope_project_id = EXCLUDED.scope_project_id,
                scope_principal_id = EXCLUDED.scope_principal_id,
                scope_role_id = EXCLUDED.scope_role_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("principal_id", Scenario0001.PrincipalId);
        command.Parameters.Add("content", NpgsqlDbType.Jsonb).Value = contentJson;
        command.Parameters.AddWithValue("content_hash", ComputeSha256(contentJson));
        command.Parameters.AddWithValue("redaction_status", redactionStatus);
        command.Parameters.AddWithValue("trust_level", trustLevel);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.Add("scope_org_id", NpgsqlDbType.Uuid).Value =
            scopeOrgId.HasValue ? scopeOrgId.Value : DBNull.Value;
        command.Parameters.Add("scope_project_id", NpgsqlDbType.Uuid).Value =
            scopeProjectId.HasValue ? scopeProjectId.Value : DBNull.Value;
        command.Parameters.Add("scope_principal_id", NpgsqlDbType.Uuid).Value =
            scopePrincipalId.HasValue ? scopePrincipalId.Value : DBNull.Value;
        command.Parameters.Add("scope_role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(scopeRoleId) ? DBNull.Value : scopeRoleId;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
