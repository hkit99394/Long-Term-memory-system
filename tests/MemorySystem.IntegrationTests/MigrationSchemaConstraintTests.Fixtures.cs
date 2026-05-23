using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
    private static async Task<Guid> InsertProjectMemoryFactAsync(
        NpgsqlConnection connection,
        ProjectFixtureIds ids)
    {
        var memoryFactId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_facts (
                id,
                scope_type,
                scope_id,
                namespace,
                project_id,
                org_id,
                memory_type,
                visibility,
                subject,
                predicate,
                object,
                confidence,
                status,
                source_event_id
            )
            VALUES (
                @memory_fact_id,
                'project',
                @project_id_text,
                @namespace,
                @project_id,
                @org_id,
                'project_decision',
                'project_shared',
                'storage',
                'uses',
                'postgres',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
        command.Parameters.AddWithValue("namespace", $"/project/{ids.ProjectId}/decisions");
        command.Parameters.AddWithValue("project_id", ids.ProjectId);
        command.Parameters.AddWithValue("org_id", ids.OrgId);
        command.Parameters.AddWithValue("event_id", ids.EventId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }

    private static async Task<Guid> InsertOrgMemoryFactAsync(
        NpgsqlConnection connection,
        ProjectFixtureIds ids)
    {
        var memoryFactId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_facts (
                id,
                scope_type,
                scope_id,
                namespace,
                org_id,
                memory_type,
                visibility,
                subject,
                predicate,
                object,
                confidence,
                status,
                source_event_id
            )
            VALUES (
                @memory_fact_id,
                'org',
                @org_id_text,
                @namespace,
                @org_id,
                'project_decision',
                'org_shared',
                'data boundaries',
                'are',
                'mandatory',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("org_id_text", ids.OrgId.ToString());
        command.Parameters.AddWithValue("namespace", $"/org/{ids.OrgId}/principles");
        command.Parameters.AddWithValue("org_id", ids.OrgId);
        command.Parameters.AddWithValue("event_id", ids.EventId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }

    private static async Task<Guid> InsertGlobalMemoryFactAsync(
        NpgsqlConnection connection,
        Guid eventId)
    {
        var memoryFactId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_facts (
                id,
                scope_type,
                scope_id,
                namespace,
                memory_type,
                visibility,
                subject,
                predicate,
                object,
                confidence,
                status,
                source_event_id
            )
            VALUES (
                @memory_fact_id,
                'global',
                'global',
                '/global/role-principles',
                'project_decision',
                'system',
                'source boundaries',
                'must be',
                'auditable',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("event_id", eventId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }

    private static async Task InsertRoleAssignmentAsync(
        NpgsqlConnection connection,
        Guid principalId,
        string scopeType,
        Guid scopeId)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO role_assignments (
                id,
                principal_id,
                role_id,
                scope_type,
                scope_id
            )
            VALUES (
                @assignment_id,
                @principal_id,
                'cto',
                @scope_type,
                @scope_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("assignment_id", Guid.NewGuid());
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> InsertProjectRoleMemoryLensAsync(
        NpgsqlConnection connection,
        ProjectFixtureIds ids,
        Guid baseMemoryFactId)
    {
        var lensId = Guid.NewGuid();

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
                source_event_id
            )
            VALUES (
                @lens_id,
                'cto',
                'project',
                @project_id_text,
                @org_id,
                @project_id,
                @base_memory_fact_id,
                'The CTO view treats PostgreSQL as a risk-reduction decision.',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("lens_id", lensId);
        command.Parameters.AddWithValue("project_id_text", ids.ProjectId.ToString());
        command.Parameters.AddWithValue("org_id", ids.OrgId);
        command.Parameters.AddWithValue("project_id", ids.ProjectId);
        command.Parameters.AddWithValue("base_memory_fact_id", baseMemoryFactId);
        command.Parameters.AddWithValue("event_id", ids.EventId);

        await command.ExecuteNonQueryAsync();

        return lensId;
    }

    private static async Task<Guid> InsertOrgRoleMemoryLensAsync(
        NpgsqlConnection connection,
        ProjectFixtureIds ids,
        Guid baseMemoryFactId)
    {
        var lensId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO role_memory_lenses (
                id,
                role_id,
                scope_type,
                scope_id,
                org_id,
                base_memory_fact_id,
                interpretation,
                confidence,
                status,
                source_event_id
            )
            VALUES (
                @lens_id,
                'cto',
                'org',
                @org_id_text,
                @org_id,
                @base_memory_fact_id,
                'The CTO view treats org data boundaries as mandatory.',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("lens_id", lensId);
        command.Parameters.AddWithValue("org_id_text", ids.OrgId.ToString());
        command.Parameters.AddWithValue("org_id", ids.OrgId);
        command.Parameters.AddWithValue("base_memory_fact_id", baseMemoryFactId);
        command.Parameters.AddWithValue("event_id", ids.EventId);

        await command.ExecuteNonQueryAsync();

        return lensId;
    }

    private static async Task<Guid> InsertGlobalRoleMemoryLensAsync(
        NpgsqlConnection connection,
        Guid eventId,
        Guid baseMemoryFactId)
    {
        var lensId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO role_memory_lenses (
                id,
                role_id,
                scope_type,
                scope_id,
                base_memory_fact_id,
                interpretation,
                confidence,
                status,
                source_event_id
            )
            VALUES (
                @lens_id,
                'cto',
                'global',
                'global',
                @base_memory_fact_id,
                'The CTO view treats auditable source boundaries as a shared principle.',
                0.900,
                'active',
                @event_id
            );
            """,
            connection);

        command.Parameters.AddWithValue("lens_id", lensId);
        command.Parameters.AddWithValue("base_memory_fact_id", baseMemoryFactId);
        command.Parameters.AddWithValue("event_id", eventId);

        await command.ExecuteNonQueryAsync();

        return lensId;
    }

    private static async Task<ProjectFixtureIds> InsertProjectFixtureAsync(
        NpgsqlConnection connection,
        bool includeSourceEvent = true)
    {
        var principalId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO principals (id, principal_type, display_name, status)
            VALUES (@principal_id, 'human', 'Test Principal', 'active');

            INSERT INTO organizations (id, name)
            VALUES (@org_id, 'Primary Org'), (@other_org_id, 'Other Org');

            INSERT INTO projects (id, org_id, name, status)
            VALUES (@project_id, @org_id, 'Project', 'active');
            """,
            connection);

        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("other_org_id", otherOrgId);
        command.Parameters.AddWithValue("project_id", projectId);

        await command.ExecuteNonQueryAsync();

        if (includeSourceEvent)
        {
            await using var eventCommand = new NpgsqlCommand(
                """
                INSERT INTO events (
                    id,
                    principal_id,
                    scope_type,
                    scope_id,
                    scope_org_id,
                    scope_project_id,
                    event_type,
                    content,
                    trust_level
                )
                VALUES (
                    @event_id,
                    @principal_id,
                    'project',
                    @project_id_text,
                    @org_id,
                    @project_id,
                    'user_message',
                    '{}'::jsonb,
                    'user_scoped'
                );
                """,
                connection);

            eventCommand.Parameters.AddWithValue("event_id", eventId);
            eventCommand.Parameters.AddWithValue("principal_id", principalId);
            eventCommand.Parameters.AddWithValue("project_id_text", projectId.ToString());
            eventCommand.Parameters.AddWithValue("org_id", orgId);
            eventCommand.Parameters.AddWithValue("project_id", projectId);

            await eventCommand.ExecuteNonQueryAsync();
        }

        return new ProjectFixtureIds(principalId, orgId, otherOrgId, projectId, eventId);
    }

    private sealed record ProjectFixtureIds(
        Guid PrincipalId,
        Guid OrgId,
        Guid OtherOrgId,
        Guid ProjectId,
        Guid EventId);
}
