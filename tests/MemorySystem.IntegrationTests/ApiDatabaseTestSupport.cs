using MemorySystem.Infrastructure.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

internal static class ApiDatabaseTestSupport
{
    public static async Task ApplyMigrationsAsync(string connectionString)
    {
        await SqlMigrationRunner.ApplyAsync(connectionString, MigrationTestPaths.FindMigrationsDirectory());
    }

    public static async Task InsertPrincipalAsync(
        string connectionString,
        Guid principalId,
        string principalType = "human",
        string displayName = "Local Demo User")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO principals (
                id,
                principal_type,
                display_name,
                status
            )
            VALUES (
                @principal_id,
                @principal_type,
                @display_name,
                'active'
            );
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("principal_type", principalType);
        command.Parameters.AddWithValue("display_name", displayName);

        await command.ExecuteNonQueryAsync();
    }

    public static async Task InsertOrganizationAndProjectAsync(
        string connectionString,
        Guid orgId,
        Guid projectId,
        string organizationName = "Memory Lab",
        string projectName = "Long-Term Memory System",
        string projectStatus = "active")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO organizations (id, name)
            VALUES (@org_id, @organization_name);

            INSERT INTO projects (id, org_id, name, status)
            VALUES (@project_id, @org_id, @project_name, @project_status);
            """,
            connection);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("organization_name", organizationName);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("project_name", projectName);
        command.Parameters.AddWithValue("project_status", projectStatus);

        await command.ExecuteNonQueryAsync();
    }

    public static async Task InsertOrganizationMembershipAsync(
        string connectionString,
        Guid orgId,
        Guid principalId,
        string accessLevel = "contributor")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO organization_memberships (org_id, principal_id, access_level)
            VALUES (@org_id, @principal_id, @access_level);
            """,
            connection);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("access_level", accessLevel);

        await command.ExecuteNonQueryAsync();
    }

    public static async Task InsertProjectMembershipAsync(
        string connectionString,
        Guid projectId,
        Guid principalId,
        string accessLevel = "contributor")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO project_memberships (project_id, principal_id, access_level)
            VALUES (@project_id, @principal_id, @access_level);
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("access_level", accessLevel);

        await command.ExecuteNonQueryAsync();
    }

    public static async Task InsertRoleAssignmentAsync(
        string connectionString,
        Guid principalId,
        string roleId,
        string scopeType = "global",
        Guid? scopeId = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO role_assignments (id, principal_id, role_id, scope_type, scope_id)
            VALUES (@id, @principal_id, @role_id, @scope_type, @scope_id);
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.Add("scope_id", NpgsqlDbType.Uuid).Value =
            scopeId.HasValue ? scopeId.Value : DBNull.Value;

        await command.ExecuteNonQueryAsync();
    }

    public static async Task InsertMemoryAccessGrantAsync(
        string connectionString,
        string namespacePrefix,
        string permission,
        Guid? principalId = null,
        string? roleId = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_access_grants (
                id,
                principal_id,
                role_id,
                namespace_prefix,
                permission
            )
            VALUES (
                @id,
                @principal_id,
                @role_id,
                @namespace_prefix,
                @permission
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.Add("principal_id", NpgsqlDbType.Uuid).Value =
            principalId.HasValue ? principalId.Value : DBNull.Value;
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(roleId) ? DBNull.Value : roleId;
        command.Parameters.AddWithValue("namespace_prefix", namespacePrefix);
        command.Parameters.AddWithValue("permission", permission);

        await command.ExecuteNonQueryAsync();
    }

    public static async Task InsertSourceEventAsync(
        string connectionString,
        Guid eventId,
        Guid principalId,
        string scopeType,
        string scopeId,
        Guid? scopeOrgId = null,
        Guid? scopeProjectId = null,
        string? scopeRoleId = null,
        string trustLevel = "user_scoped",
        string sensitivity = "none",
        string retentionClass = "standard",
        string redactionStatus = "none")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

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
                'sha256:test',
                @retention_class,
                @sensitivity,
                @redaction_status,
                @trust_level,
                @scope_type,
                @scope_id,
                @scope_org_id,
                @scope_project_id,
                @scope_principal_id,
                @scope_role_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.Add("content", NpgsqlDbType.Jsonb).Value = """{"message":"source"}""";
        command.Parameters.AddWithValue("retention_class", retentionClass);
        command.Parameters.AddWithValue("sensitivity", sensitivity);
        command.Parameters.AddWithValue("redaction_status", redactionStatus);
        command.Parameters.AddWithValue("trust_level", trustLevel);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.Add("scope_org_id", NpgsqlDbType.Uuid).Value =
            scopeOrgId.HasValue ? scopeOrgId.Value : DBNull.Value;
        command.Parameters.Add("scope_project_id", NpgsqlDbType.Uuid).Value =
            scopeProjectId.HasValue ? scopeProjectId.Value : DBNull.Value;
        command.Parameters.Add("scope_principal_id", NpgsqlDbType.Uuid).Value =
            scopeType is "user" or "agent" ? Guid.Parse(scopeId) : DBNull.Value;
        command.Parameters.Add("scope_role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(scopeRoleId) ? DBNull.Value : scopeRoleId;

        await command.ExecuteNonQueryAsync();
    }

    public static async Task InsertLegacyAgentSourceEventAsync(
        string connectionString,
        Guid eventId,
        Guid agentPrincipalId,
        string scopeType,
        string scopeId,
        Guid? scopeOrgId = null,
        Guid? scopeProjectId = null,
        string? scopeRoleId = null,
        string trustLevel = "agent_private",
        string sensitivity = "none",
        string retentionClass = "standard",
        string redactionStatus = "none")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO events (
                id,
                agent_principal_id,
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
                @agent_principal_id,
                'assistant_message',
                @content,
                'sha256:test',
                @retention_class,
                @sensitivity,
                @redaction_status,
                @trust_level,
                @scope_type,
                @scope_id,
                @scope_org_id,
                @scope_project_id,
                @scope_principal_id,
                @scope_role_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("agent_principal_id", agentPrincipalId);
        command.Parameters.Add("content", NpgsqlDbType.Jsonb).Value = """{"message":"legacy agent source"}""";
        command.Parameters.AddWithValue("retention_class", retentionClass);
        command.Parameters.AddWithValue("sensitivity", sensitivity);
        command.Parameters.AddWithValue("redaction_status", redactionStatus);
        command.Parameters.AddWithValue("trust_level", trustLevel);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.Add("scope_org_id", NpgsqlDbType.Uuid).Value =
            scopeOrgId.HasValue ? scopeOrgId.Value : DBNull.Value;
        command.Parameters.Add("scope_project_id", NpgsqlDbType.Uuid).Value =
            scopeProjectId.HasValue ? scopeProjectId.Value : DBNull.Value;
        command.Parameters.Add("scope_principal_id", NpgsqlDbType.Uuid).Value =
            scopeType is "user" or "agent" ? Guid.Parse(scopeId) : DBNull.Value;
        command.Parameters.Add("scope_role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(scopeRoleId) ? DBNull.Value : scopeRoleId;

        await command.ExecuteNonQueryAsync();
    }

    public static async Task<int> CountRowsAsync(string connectionString, string tableName)
    {
        if (!AllowedCountTables.Contains(tableName))
        {
            throw new ArgumentException($"Table '{tableName}' is not supported by the test row counter.", nameof(tableName));
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {tableName};", connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public static async Task<int> CountIdempotencyRecordsAsync(string connectionString)
    {
        return await CountRowsAsync(connectionString, "api_idempotency_keys");
    }

    public static async Task<ApiIdempotencyRecordSummary> ReadSingleIdempotencySummaryAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT endpoint, idempotency_key, status, response_status, resource_type, resource_id
            FROM api_idempotency_keys;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new ApiIdempotencyRecordSummary(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5));
    }

    public static async Task<IReadOnlyList<ApiIdempotencyRecordDetail>> ReadIdempotencyDetailsAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                principal_id,
                endpoint,
                idempotency_key,
                request_hash,
                response_status,
                response_body::text,
                response_content_type,
                status,
                expires_at
            FROM api_idempotency_keys
            ORDER BY principal_id;
            """,
            connection);

        var records = new List<ApiIdempotencyRecordDetail>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            records.Add(new ApiIdempotencyRecordDetail(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.GetFieldValue<DateTimeOffset>(8)));
        }

        return records;
    }

    private static readonly IReadOnlySet<string> AllowedCountTables = new HashSet<string>(StringComparer.Ordinal)
    {
        "api_idempotency_keys",
        "events",
        "memory_chunks",
        "memory_facts",
        "outbox_jobs",
        "role_memory_lenses"
    };
}

internal sealed record ApiIdempotencyRecordSummary(
    string Endpoint,
    string IdempotencyKey,
    string Status,
    int ResponseStatus,
    string? ResourceType,
    Guid? ResourceId);

internal sealed record ApiIdempotencyRecordDetail(
    Guid PrincipalId,
    string Endpoint,
    string IdempotencyKey,
    string RequestHash,
    int? ResponseStatus,
    string? ResponseBody,
    string? ResponseContentType,
    string Status,
    DateTimeOffset ExpiresAt);
