using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MemorySystem.Application.AccessAuditing;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminProjectLifecycleSettingsTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OtherPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OrgId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Project_lifecycle_and_scope_settings_update_with_payload_safe_audit_evidence()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm03_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = MemorySystemApiTestFactory.Create(
                databaseConnectionString,
                TestApiKey,
                ActorPrincipalId.ToString("D"));
            using var client = factory.CreateClient();

            using var defaultSettingsResponse = await client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/api/admin/projects/{ProjectId:D}/scope-settings"));
            using var defaultSettingsPayload = await ReadJsonAsync(defaultSettingsResponse);
            Assert.Equal(HttpStatusCode.OK, defaultSettingsResponse.StatusCode);
            Assert.Equal("OPM-03", defaultSettingsPayload.RootElement.GetProperty("contractId").GetString());
            var defaultSettings = defaultSettingsPayload.RootElement.GetProperty("scopeSettings");
            Assert.True(defaultSettings.GetProperty("isDefault").GetBoolean());
            Assert.Equal($"/project/{ProjectId:D}/facts", defaultSettings.GetProperty("defaultNamespacePrefix").GetString());

            using var lifecycleResponse = await client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Patch,
                $"/api/admin/projects/{ProjectId:D}/lifecycle",
                new
                {
                    projectStatus = "archived",
                    reason = "Pilot complete",
                    auditEvidenceId = "opm03-lifecycle-001"
                }));
            using var lifecyclePayload = await ReadJsonAsync(lifecycleResponse);
            Assert.Equal(HttpStatusCode.OK, lifecycleResponse.StatusCode);
            Assert.Equal("OPM-03", lifecyclePayload.RootElement.GetProperty("contractId").GetString());
            Assert.Equal("updated", lifecyclePayload.RootElement.GetProperty("status").GetString());
            Assert.Equal("active", lifecyclePayload.RootElement.GetProperty("previousProjectStatus").GetString());
            Assert.Equal("archived", lifecyclePayload.RootElement.GetProperty("project").GetProperty("projectStatus").GetString());
            Assert.Equal("project_lifecycle_change", lifecyclePayload.RootElement.GetProperty("auditEvidence").GetProperty("actionType").GetString());
            Assert.True(lifecyclePayload.RootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(lifecyclePayload.RootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());

            using var settingsResponse = await client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Put,
                $"/api/admin/projects/{ProjectId:D}/scope-settings",
                new
                {
                    defaultNamespacePrefix = $"/project/{ProjectId:D}/release-evidence",
                    sourceHashRequired = true,
                    memoryRetentionClass = "audit",
                    reviewCadenceDays = 14,
                    reason = "Align review cadence with pilot closeout",
                    auditEvidenceId = "opm03-settings-001"
                }));
            using var settingsPayload = await ReadJsonAsync(settingsResponse);
            Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
            Assert.Equal("OPM-03", settingsPayload.RootElement.GetProperty("contractId").GetString());
            Assert.Equal("project_scope_settings_change", settingsPayload.RootElement.GetProperty("auditEvidence").GetProperty("actionType").GetString());
            var storedSettings = settingsPayload.RootElement.GetProperty("scopeSettings");
            Assert.False(storedSettings.GetProperty("isDefault").GetBoolean());
            Assert.Equal($"/project/{ProjectId:D}/release-evidence", storedSettings.GetProperty("defaultNamespacePrefix").GetString());
            Assert.True(storedSettings.GetProperty("sourceHashRequired").GetBoolean());
            Assert.Equal("audit", storedSettings.GetProperty("memoryRetentionClass").GetString());
            Assert.Equal(14, storedSettings.GetProperty("reviewCadenceDays").GetInt32());

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Equal("archived", await ReadProjectStatusAsync(dataSource));
            var settings = await ReadStoredSettingsAsync(dataSource);
            Assert.Equal($"/project/{ProjectId:D}/release-evidence", settings.DefaultNamespacePrefix);
            Assert.True(settings.SourceHashRequired);
            Assert.Equal("audit", settings.MemoryRetentionClass);
            Assert.Equal(14, settings.ReviewCadenceDays);
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.ProjectLifecycleChange, ActorPrincipalId));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.ProjectScopeSettingsChange, ActorPrincipalId));
            Assert.Equal("opm03-lifecycle-001", await ReadAuditMetadataAsync(dataSource, AccessAuditActionTypes.ProjectLifecycleChange, "auditEvidenceId"));
            Assert.Equal("opm03-settings-001", await ReadAuditMetadataAsync(dataSource, AccessAuditActionTypes.ProjectScopeSettingsChange, "auditEvidenceId"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Project_lifecycle_update_rejects_forbidden_operator_without_changing_status()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_opm03_forbidden_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareFixtureAsync(databaseConnectionString);

            using var factory = MemorySystemApiTestFactory.Create(
                databaseConnectionString,
                TestApiKey,
                OtherPrincipalId.ToString("D"));
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Patch,
                $"/api/admin/projects/{ProjectId:D}/lifecycle",
                new
                {
                    projectStatus = "archived",
                    reason = "Unauthorized attempt",
                    auditEvidenceId = "opm03-denied-001"
                }));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("Actor must", body, StringComparison.Ordinal);
            Assert.DoesNotContain("parent organization", body, StringComparison.OrdinalIgnoreCase);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Equal("active", await ReadProjectStatusAsync(dataSource));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.AuthorizationDenied, OtherPrincipalId));
            Assert.Equal(0L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.ProjectLifecycleChange, OtherPrincipalId));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task PrepareFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ActorPrincipalId, displayName: "Organization Owner");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, OtherPrincipalId, displayName: "No Access");
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            OrgId,
            ProjectId,
            organizationName: "OPM Organization",
            projectName: "Lifecycle Project",
            projectStatus: "active");
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Api-Key", TestApiKey);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    private static async Task<string?> ReadProjectStatusAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand("SELECT status FROM projects WHERE id = @project_id;");
        command.Parameters.AddWithValue("project_id", ProjectId);
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<ProjectScopeSettingsRow> ReadStoredSettingsAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT default_namespace_prefix, source_hash_required, memory_retention_class, review_cadence_days
            FROM project_scope_settings
            WHERE project_id = @project_id;
            """);
        command.Parameters.AddWithValue("project_id", ProjectId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new ProjectScopeSettingsRow(
            reader.GetString(0),
            reader.GetBoolean(1),
            reader.GetString(2),
            reader.GetInt32(3));
    }

    private static async Task<long> CountAuditEventsAsync(
        NpgsqlDataSource dataSource,
        string actionType,
        Guid actorPrincipalId)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT count(*)
            FROM access_audit_events
            WHERE action_type = @action_type
                AND actor_principal_id = @actor_principal_id;
            """);
        command.Parameters.AddWithValue("action_type", actionType);
        command.Parameters.AddWithValue("actor_principal_id", actorPrincipalId);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<string?> ReadAuditMetadataAsync(
        NpgsqlDataSource dataSource,
        string actionType,
        string metadataKey)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT audit_metadata->>@metadata_key
            FROM access_audit_events
            WHERE action_type = @action_type
            ORDER BY occurred_at DESC
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("action_type", actionType);
        command.Parameters.AddWithValue("metadata_key", metadataKey);
        return await command.ExecuteScalarAsync() as string;
    }

    private sealed record ProjectScopeSettingsRow(
        string DefaultNamespacePrefix,
        bool SourceHashRequired,
        string MemoryRetentionClass,
        int ReviewCadenceDays);
}
