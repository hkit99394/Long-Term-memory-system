using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_rejects_existing_memory_chunk_with_mismatched_source_scope()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_existing_chunk_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                var eventId = Guid.NewGuid();
                var memoryFactId = Guid.NewGuid();
                var principalId = Guid.NewGuid();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@principal_id, 'human', 'Legacy Chunk Principal', 'active');

                    INSERT INTO events (
                        id,
                        principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @event_id,
                        @principal_id,
                        'memory_written',
                        '{}'::jsonb,
                        'human_approved'
                    );

                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        user_principal_id,
                        memory_type,
                        visibility,
                        subject,
                        predicate,
                        object,
                        confidence,
                        status,
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @memory_fact_id,
                        'user',
                        @principal_id_text,
                        @namespace,
                        @principal_id,
                        'project_decision',
                        'private',
                        'storage',
                        'uses',
                        'postgres',
                        0.900,
                        'active',
                        @event_id,
                        @principal_id
                    );

                    INSERT INTO memory_chunks (
                        id,
                        source_type,
                        source_id,
                        namespace,
                        scope_type,
                        scope_id,
                        content,
                        content_hash,
                        trust_level,
                        source_event_id
                    )
                    VALUES (
                        @chunk_id,
                        'memory_fact',
                        @memory_fact_id,
                        @wrong_namespace,
                        'user',
                        @principal_id_text,
                        'PostgreSQL stores long-term memory facts.',
                        'existing-bad-chunk-hash',
                        'human_approved',
                        @event_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("event_id", eventId);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("principal_id", principalId);
                command.Parameters.AddWithValue("principal_id_text", principalId.ToString());
                command.Parameters.AddWithValue("namespace", $"/user/{principalId}/facts");
                command.Parameters.AddWithValue("wrong_namespace", $"/user/{principalId}/other");
                command.Parameters.AddWithValue("chunk_id", Guid.NewGuid());

                await command.ExecuteNonQueryAsync();
            }

            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory()));

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("existing memory_chunks", exception.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_rejects_existing_event_with_ambiguous_legacy_scope_signals()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_ambiguous_event_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                var principalId = Guid.NewGuid();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@principal_id, 'human', 'Ambiguous Event Principal', 'active');

                    INSERT INTO events (
                        id,
                        principal_id,
                        conversation_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @event_id,
                        @principal_id,
                        @conversation_id,
                        'user_message',
                        '{}'::jsonb,
                        'user_scoped'
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("principal_id", principalId);
                command.Parameters.AddWithValue("event_id", Guid.NewGuid());
                command.Parameters.AddWithValue("conversation_id", Guid.NewGuid());

                await command.ExecuteNonQueryAsync();
            }

            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory()));

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("ambiguous legacy scope signals", exception.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_backfills_existing_event_scope_from_legacy_columns()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_event_backfill_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            var userPrincipalId = Guid.NewGuid();
            var agentPrincipalId = Guid.NewGuid();
            var userEventId = Guid.NewGuid();
            var agentEventId = Guid.NewGuid();
            var roleEventId = Guid.NewGuid();
            var sessionEventId = Guid.NewGuid();
            var globalEventId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES
                        (@user_principal_id, 'human', 'Event User', 'active'),
                        (@agent_principal_id, 'agent', 'Event Agent', 'active');

                    INSERT INTO events (
                        id,
                        principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @user_event_id,
                        @user_principal_id,
                        'user_message',
                        '{}'::jsonb,
                        'user_scoped'
                    );

                    INSERT INTO events (
                        id,
                        agent_principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @agent_event_id,
                        @agent_principal_id,
                        'assistant_message',
                        '{}'::jsonb,
                        'agent_private'
                    );

                    INSERT INTO events (
                        id,
                        role_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @role_event_id,
                        'cto',
                        'memory_reviewed',
                        '{}'::jsonb,
                        'human_approved'
                    );

                    INSERT INTO events (
                        id,
                        conversation_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @session_event_id,
                        @conversation_id,
                        'user_message',
                        '{}'::jsonb,
                        'user_scoped'
                    );

                    INSERT INTO events (
                        id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @global_event_id,
                        'tool_call',
                        '{}'::jsonb,
                        'system_trusted'
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("user_principal_id", userPrincipalId);
                command.Parameters.AddWithValue("agent_principal_id", agentPrincipalId);
                command.Parameters.AddWithValue("user_event_id", userEventId);
                command.Parameters.AddWithValue("agent_event_id", agentEventId);
                command.Parameters.AddWithValue("role_event_id", roleEventId);
                command.Parameters.AddWithValue("session_event_id", sessionEventId);
                command.Parameters.AddWithValue("global_event_id", globalEventId);
                command.Parameters.AddWithValue("conversation_id", conversationId);

                await command.ExecuteNonQueryAsync();
            }

            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                var userScope = await ReadEventScopeAsync(connection, userEventId);
                var agentScope = await ReadEventScopeAsync(connection, agentEventId);
                var roleScope = await ReadEventScopeAsync(connection, roleEventId);
                var sessionScope = await ReadEventScopeAsync(connection, sessionEventId);
                var globalScope = await ReadEventScopeAsync(connection, globalEventId);

                Assert.Equal(new EventScope("user", userPrincipalId.ToString(), userPrincipalId.ToString(), null), userScope);
                Assert.Equal(new EventScope("agent", agentPrincipalId.ToString(), agentPrincipalId.ToString(), null), agentScope);
                Assert.Equal(new EventScope("role", "cto", null, "cto"), roleScope);
                Assert.Equal(new EventScope("session", conversationId.ToString(), null, null), sessionScope);
                Assert.Equal(new EventScope("global", "global", null, null), globalScope);
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_reconciles_legacy_principal_events_for_non_user_memory_scopes()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_legacy_event_scope_reconcile_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            var principalId = Guid.NewGuid();
            var orgId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var projectFactEventId = Guid.NewGuid();
            var orgFactEventId = Guid.NewGuid();
            var globalFactEventId = Guid.NewGuid();
            var lensEventId = Guid.NewGuid();
            var projectMemoryFactId = Guid.NewGuid();
            var orgMemoryFactId = Guid.NewGuid();
            var globalMemoryFactId = Guid.NewGuid();
            var lensId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@principal_id, 'human', 'Legacy Scope Principal', 'active');

                    INSERT INTO organizations (id, name)
                    VALUES (@org_id, 'Legacy Scope Org');

                    INSERT INTO projects (id, org_id, name, status)
                    VALUES (@project_id, @org_id, 'Legacy Scope Project', 'active');

                    INSERT INTO events (
                        id,
                        principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES
                        (@project_fact_event_id, @principal_id, 'memory_written', '{}'::jsonb, 'user_scoped'),
                        (@org_fact_event_id, @principal_id, 'memory_written', '{}'::jsonb, 'user_scoped'),
                        (@global_fact_event_id, @principal_id, 'memory_written', '{}'::jsonb, 'user_scoped'),
                        (@lens_event_id, @principal_id, 'memory_reviewed', '{}'::jsonb, 'human_approved');

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
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @project_memory_fact_id,
                        'project',
                        @project_id_text,
                        @project_namespace,
                        @project_id,
                        @org_id,
                        'project_decision',
                        'project_shared',
                        'storage',
                        'uses',
                        'postgres',
                        0.900,
                        'active',
                        @project_fact_event_id,
                        NULL
                    );

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
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @org_memory_fact_id,
                        'org',
                        @org_id_text,
                        @org_namespace,
                        @org_id,
                        'project_decision',
                        'org_shared',
                        'retention',
                        'requires',
                        'auditability',
                        0.900,
                        'active',
                        @org_fact_event_id,
                        @principal_id
                    );

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
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @global_memory_fact_id,
                        'global',
                        'global',
                        '/global/facts',
                        'fact',
                        'system',
                        'source boundaries',
                        'are',
                        'auditable',
                        0.900,
                        'active',
                        @global_fact_event_id,
                        @principal_id
                    );

                    INSERT INTO role_memory_lenses (
                        id,
                        role_id,
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
                        @project_id,
                        @project_memory_fact_id,
                        'The CTO view treats PostgreSQL as an auditability decision.',
                        0.900,
                        'active',
                        @lens_event_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("principal_id", principalId);
                command.Parameters.AddWithValue("org_id", orgId);
                command.Parameters.AddWithValue("project_id", projectId);
                command.Parameters.AddWithValue("project_id_text", projectId.ToString());
                command.Parameters.AddWithValue("org_id_text", orgId.ToString());
                command.Parameters.AddWithValue("project_namespace", $"/project/{projectId}/decisions");
                command.Parameters.AddWithValue("org_namespace", $"/org/{orgId}/principles");
                command.Parameters.AddWithValue("project_fact_event_id", projectFactEventId);
                command.Parameters.AddWithValue("org_fact_event_id", orgFactEventId);
                command.Parameters.AddWithValue("global_fact_event_id", globalFactEventId);
                command.Parameters.AddWithValue("lens_event_id", lensEventId);
                command.Parameters.AddWithValue("project_memory_fact_id", projectMemoryFactId);
                command.Parameters.AddWithValue("org_memory_fact_id", orgMemoryFactId);
                command.Parameters.AddWithValue("global_memory_fact_id", globalMemoryFactId);
                command.Parameters.AddWithValue("lens_id", lensId);

                await command.ExecuteNonQueryAsync();
            }

            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                Assert.Equal(new EventScope("project", projectId.ToString(), null, null), await ReadEventScopeAsync(connection, projectFactEventId));
                Assert.Equal(new EventScope("org", orgId.ToString(), null, null), await ReadEventScopeAsync(connection, orgFactEventId));
                Assert.Equal(new EventScope("global", "global", null, null), await ReadEventScopeAsync(connection, globalFactEventId));
                Assert.Equal(new EventScope("project", projectId.ToString(), null, null), await ReadEventScopeAsync(connection, lensEventId));

                await using var lensCommand = new NpgsqlCommand(
                    """
                    SELECT proposed_by_principal_id
                    FROM role_memory_lenses
                    WHERE id = @lens_id;
                    """,
                    connection);
                lensCommand.Parameters.AddWithValue("lens_id", lensId);

                Assert.Equal(principalId, await lensCommand.ExecuteScalarAsync());

                await using var memoryFactCommand = new NpgsqlCommand(
                    """
                    SELECT proposed_by_principal_id
                    FROM memory_facts
                    WHERE id = @memory_fact_id;
                    """,
                    connection);
                memoryFactCommand.Parameters.AddWithValue("memory_fact_id", projectMemoryFactId);

                Assert.Equal(principalId, await memoryFactCommand.ExecuteScalarAsync());
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_preserves_legacy_agent_scoped_memory_fact_provenance()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_agent_fact_provenance_upgrade_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            var agentPrincipalId = Guid.NewGuid();
            var agentEventId = Guid.NewGuid();
            var memoryFactId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@agent_principal_id, 'agent', 'Legacy Agent Principal', 'active');

                    INSERT INTO events (
                        id,
                        agent_principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @agent_event_id,
                        @agent_principal_id,
                        'assistant_message',
                        '{}'::jsonb,
                        'agent_private'
                    );

                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        agent_principal_id,
                        memory_type,
                        visibility,
                        subject,
                        predicate,
                        object,
                        confidence,
                        status,
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @memory_fact_id,
                        'agent',
                        @agent_principal_id_text,
                        @namespace,
                        @agent_principal_id,
                        'agent_private',
                        'private',
                        'indexing worker',
                        'prefers',
                        'short leases',
                        0.900,
                        'active',
                        @agent_event_id,
                        @agent_principal_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("agent_principal_id", agentPrincipalId);
                command.Parameters.AddWithValue("agent_principal_id_text", agentPrincipalId.ToString());
                command.Parameters.AddWithValue("agent_event_id", agentEventId);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("namespace", $"/agent/{agentPrincipalId}/private");

                await command.ExecuteNonQueryAsync();
            }

            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    SELECT proposed_by_principal_id, trust_level
                    FROM memory_facts
                    WHERE id = @memory_fact_id;
                    """,
                    connection);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);

                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(agentPrincipalId, reader.GetGuid(0));
                Assert.Equal("agent_private", reader.GetString(1));
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_backfills_actorless_legacy_source_events_to_system_provenance_principal()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_actorless_provenance_upgrade_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            var globalFactEventId = Guid.NewGuid();
            var lensEventId = Guid.NewGuid();
            var memoryFactId = Guid.NewGuid();
            var lensId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO events (
                        id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES
                        (@global_fact_event_id, 'memory_written', '{}'::jsonb, 'system_trusted'),
                        (@lens_event_id, 'memory_reviewed', '{}'::jsonb, 'system_trusted');

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
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @memory_fact_id,
                        'global',
                        'global',
                        '/global/facts',
                        'fact',
                        'system',
                        'source boundaries',
                        'are',
                        'auditable',
                        0.900,
                        'active',
                        @global_fact_event_id,
                        NULL
                    );

                    INSERT INTO role_memory_lenses (
                        id,
                        role_id,
                        base_memory_fact_id,
                        interpretation,
                        confidence,
                        status,
                        source_event_id
                    )
                    VALUES (
                        @lens_id,
                        'cto',
                        @memory_fact_id,
                        'The CTO view treats source boundaries as an auditability principle.',
                        0.900,
                        'active',
                        @lens_event_id
                    );
                    """,
                    connection);

                command.Parameters.AddWithValue("global_fact_event_id", globalFactEventId);
                command.Parameters.AddWithValue("lens_event_id", lensEventId);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("lens_id", lensId);

                await command.ExecuteNonQueryAsync();
            }

            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    SELECT
                        fact.proposed_by_principal_id,
                        fact.trust_level,
                        lens.proposed_by_principal_id,
                        principal.principal_type
                    FROM memory_facts AS fact
                    INNER JOIN role_memory_lenses AS lens ON lens.id = @lens_id
                    INNER JOIN principals AS principal ON principal.id = fact.proposed_by_principal_id
                    WHERE fact.id = @memory_fact_id;
                    """,
                    connection);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("lens_id", lensId);

                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(LegacySystemProvenancePrincipalId, reader.GetGuid(0));
                Assert.Equal("system_trusted", reader.GetString(1));
                Assert.Equal(LegacySystemProvenancePrincipalId, reader.GetGuid(2));
                Assert.Equal("service", reader.GetString(3));
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ApplyAsync_backfills_memory_fact_trust_level_from_source_event()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_fact_trust_backfill_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApplyInitialMigrationAsync(databaseConnectionString);

            var eventId = Guid.NewGuid();
            var memoryFactId = Guid.NewGuid();
            var principalId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO principals (id, principal_type, display_name, status)
                    VALUES (@principal_id, 'human', 'Legacy Trust Principal', 'active');

                    INSERT INTO events (
                        id,
                        principal_id,
                        event_type,
                        content,
                        trust_level
                    )
                    VALUES (
                        @event_id,
                        @principal_id,
                        'memory_written',
                        '{}'::jsonb,
                        'web_content'
                    );

                    INSERT INTO memory_facts (
                        id,
                        scope_type,
                        scope_id,
                        namespace,
                        user_principal_id,
                        memory_type,
                        visibility,
                        subject,
                        predicate,
                        object,
                        confidence,
                        status,
                        source_event_id,
                        proposed_by_principal_id
                    )
                    VALUES (
                        @memory_fact_id,
                        'user',
                        @principal_id_text,
                        @namespace,
                        @principal_id,
                        'fact',
                        'private',
                        'source trust',
                        'comes from',
                        'event',
                        0.900,
                        'active',
                        @event_id,
                        @principal_id
                    );
                    """,
                    connection);
                command.Parameters.AddWithValue("event_id", eventId);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
                command.Parameters.AddWithValue("principal_id", principalId);
                command.Parameters.AddWithValue("principal_id_text", principalId.ToString());
                command.Parameters.AddWithValue("namespace", $"/user/{principalId}/facts");

                await command.ExecuteNonQueryAsync();
            }

            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using (var connection = new NpgsqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    "SELECT trust_level FROM memory_facts WHERE id = @memory_fact_id;",
                    connection);
                command.Parameters.AddWithValue("memory_fact_id", memoryFactId);

                Assert.Equal("web_content", await command.ExecuteScalarAsync());
            }
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
