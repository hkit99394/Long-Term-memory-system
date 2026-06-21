using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MemorySystem.Application.Admin;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Infrastructure.AccessAuditing;
using MemorySystem.Infrastructure.Admin;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminProjectRegistrationTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ProductOwnerPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid KnowledgeStewardPrincipalId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid SecurityPrincipalId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ResearchLeadPrincipalId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid OrgId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid ProjectId = Guid.Parse("77777777-7777-4777-8777-777777777777");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_project_registration_requires_authentication()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_project_registration_auth_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "/api/admin/projects/register",
                CreateRegistrationBody());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_project_registration_upserts_project_access_and_audit_evidence_idempotently()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_project_registration_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareRegistrationFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/projects/register",
                CreateRegistrationBody(),
                "reg02-register-project"));
            var body = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(body);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("REG-02", payload.RootElement.GetProperty("contractId").GetString());
            Assert.Equal("registered", payload.RootElement.GetProperty("status").GetString());
            Assert.True(payload.RootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(payload.RootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
            Assert.Equal(4, payload.RootElement.GetProperty("ownerAssignments").GetArrayLength());
            Assert.Equal(4, payload.RootElement.GetProperty("namespaceGrants").GetArrayLength());
            Assert.Equal(1, payload.RootElement.GetProperty("sourceDocumentCount").GetInt32());
            Assert.Equal(100, payload.RootElement.GetProperty("sourceHashCoveragePercent").GetInt32());

            var auditEvidence = payload.RootElement.GetProperty("auditEvidence");
            var auditEventId = auditEvidence.GetProperty("auditEventId").GetGuid();
            Assert.Equal("project_registration", auditEvidence.GetProperty("actionType").GetString());
            Assert.Equal("preview-reg02-001", auditEvidence.GetProperty("accessPreviewReportId").GetString());

            using var replayResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/projects/register",
                CreateRegistrationBody(),
                "reg02-register-project"));
            var replayBody = await replayResponse.Content.ReadAsStringAsync();
            using var replayPayload = JsonDocument.Parse(replayBody);

            Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
            Assert.Equal(auditEventId, replayPayload.RootElement.GetProperty("auditEvidence").GetProperty("auditEventId").GetGuid());

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Equal("active", await ReadProjectStatusAsync(dataSource));
            Assert.True(await HasProjectRoleDefinitionAsync(dataSource, "research_lead"));
            Assert.True(await HasProjectMembershipAsync(dataSource, ProductOwnerPrincipalId, "reviewer"));
            Assert.True(await HasProjectMembershipAsync(dataSource, KnowledgeStewardPrincipalId, "reviewer"));
            Assert.True(await HasProjectMembershipAsync(dataSource, SecurityPrincipalId, "reviewer"));
            Assert.True(await HasProjectMembershipAsync(dataSource, ResearchLeadPrincipalId, "contributor"));
            Assert.True(await HasRoleAssignmentAsync(dataSource, ProductOwnerPrincipalId, "product_owner"));
            Assert.True(await HasRoleAssignmentAsync(dataSource, ResearchLeadPrincipalId, "research_lead"));
            Assert.True(await HasRoleNamespaceGrantAsync(dataSource, "product_owner", $"/project/{ProjectId}/goals", "write"));
            Assert.True(await HasRoleNamespaceGrantAsync(dataSource, "research_lead", $"/project/{ProjectId}/role/research_lead/lens", "read"));
            Assert.False(await HasProjectRootNamespaceGrantAsync(dataSource));
            Assert.False(await HasAdminNamespaceGrantAsync(dataSource));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.ProjectRegistration));
            Assert.Equal(1, await ApiDatabaseTestSupport.CountIdempotencyRecordsAsync(databaseConnectionString));
            var idempotency = await ApiDatabaseTestSupport.ReadSingleIdempotencySummaryAsync(databaseConnectionString);
            Assert.Equal("POST /api/admin/projects/register", idempotency.Endpoint);
            Assert.Equal("project_registration", idempotency.ResourceType);
            Assert.Equal(ProjectId, idempotency.ResourceId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_project_registration_forbidden_actor_is_audited_and_does_not_register_project()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_project_registration_forbidden_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareRegistrationFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString, targetActorPrincipalId: ProductOwnerPrincipalId);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/projects/register",
                CreateRegistrationBody(),
                "reg02-forbidden-project"));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("Actor must have admin access to the target project or owner/admin access to the target organization.", body, StringComparison.Ordinal);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Null(await ReadProjectStatusAsync(dataSource));
            Assert.Equal(1L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.AuthorizationDenied, ProductOwnerPrincipalId));
            Assert.Equal(0L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.ProjectRegistration, ProductOwnerPrincipalId));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_project_registration_rejects_root_or_admin_namespace_grants()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_project_registration_reject_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareRegistrationFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var unsafeBody = CreateRegistrationBody(
                namespaceGrants:
                [
                    new
                    {
                        roleId = "product_owner",
                        namespacePrefix = $"/project/{ProjectId}",
                        permission = "admin"
                    }
                ]);

            using var response = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/projects/register",
                unsafeBody,
                "reg02-reject-admin-root"));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Project root namespace grants are forbidden for registration.", body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_project_registration_rejects_overlong_audit_metadata_ids()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_project_registration_metadata_length_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareRegistrationFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var overlongId = new string('x', 501);
            using var previewResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/projects/register",
                CreateRegistrationBody(accessPreviewReportId: overlongId),
                "reg02-overlong-preview"));
            var previewBody = await previewResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, previewResponse.StatusCode);
            Assert.Contains("Access preview report id must be 500 characters or fewer.", previewBody, StringComparison.Ordinal);

            using var exportResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/projects/register",
                CreateRegistrationBody(auditExportId: overlongId),
                "reg02-overlong-export"));
            var exportBody = await exportResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, exportResponse.StatusCode);
            Assert.Contains("Audit export id must be 500 characters or fewer.", exportBody, StringComparison.Ordinal);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Null(await ReadProjectStatusAsync(dataSource));
            Assert.Equal(0L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.ProjectRegistration));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_project_registration_requires_governance_owner_roles()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_project_registration_owner_roles_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareRegistrationFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            foreach (var missingRoleId in new[] { "product_owner", "knowledge_steward", "security_professional" })
            {
                using var response = await client.SendAsync(CreateAuthenticatedJsonRequest(
                    "/api/admin/projects/register",
                    CreateRegistrationBody(ownerAssignments: CreateOwnerAssignments(missingRoleId)),
                    $"reg02-missing-owner-{missingRoleId}"));
                var body = await response.Content.ReadAsStringAsync();

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Required owner assignment roles are missing", body, StringComparison.Ordinal);
                Assert.Contains(missingRoleId, body, StringComparison.Ordinal);
            }

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Null(await ReadProjectStatusAsync(dataSource));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_admin_project_registration_requires_source_documents()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_project_registration_source_docs_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareRegistrationFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CreateAuthenticatedJsonRequest(
                "/api/admin/projects/register",
                CreateRegistrationBody(sourceDocuments: []),
                "reg02-missing-source-docs"));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("At least one source document is required.", body, StringComparison.Ordinal);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            Assert.Null(await ReadProjectStatusAsync(dataSource));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task RegisterAsync_rolls_back_project_changes_when_idempotency_completion_fails()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_project_registration_idempotency_rollback_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareRegistrationFixtureAsync(databaseConnectionString);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresAdminProjectRegistrationStore(
                dataSource,
                new PostgresAccessAuditEventStore(dataSource));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.RegisterAsync(CreateRegistrationCommandWithMissingIdempotencyRecord()));

            Assert.Null(await ReadProjectStatusAsync(dataSource));
            Assert.False(await HasProjectRoleDefinitionAsync(dataSource, "research_lead"));
            Assert.Equal(0L, await CountAuditEventsAsync(dataSource, AccessAuditActionTypes.ProjectRegistration));
            Assert.Equal(0, await ApiDatabaseTestSupport.CountIdempotencyRecordsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task PrepareRegistrationFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ActorPrincipalId, displayName: "Registration Admin");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ProductOwnerPrincipalId, displayName: "Product Owner");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, KnowledgeStewardPrincipalId, displayName: "Knowledge Steward");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, SecurityPrincipalId, displayName: "Security Owner");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ResearchLeadPrincipalId, displayName: "Research Lead");
        await InsertOrganizationAsync(connectionString);
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");
    }

    private static async Task InsertOrganizationAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO organizations (id, name)
            VALUES (@org_id, 'Existing Registration Org');
            """,
            connection);
        command.Parameters.AddWithValue("org_id", OrgId);

        await command.ExecuteNonQueryAsync();
    }

    private static object CreateRegistrationBody(
        object[]? namespaceGrants = null,
        object[]? ownerAssignments = null,
        object[]? sourceDocuments = null,
        string? accessPreviewReportId = null,
        string? auditExportId = null)
    {
        return new
        {
            organizationId = OrgId,
            organizationName = "Registered Memory Org",
            projectId = ProjectId,
            projectName = "Registered Memory Project",
            projectStatus = "active",
            roleDefinitions = new object[]
            {
                new
                {
                    roleId = "research_lead",
                    displayName = "Research Lead",
                    description = "Owns research interpretation during registration.",
                    templateRoleId = "developer",
                    status = "active"
                }
            },
            ownerAssignments = ownerAssignments ?? CreateOwnerAssignments(),
            namespaceGrants = namespaceGrants
                ?? [
                    new
                    {
                        roleId = "product_owner",
                        namespacePrefix = $"/project/{ProjectId}/goals",
                        permission = "write"
                    },
                    new
                    {
                        roleId = "knowledge_steward",
                        namespacePrefix = $"/project/{ProjectId}/facts",
                        permission = "write"
                    },
                    new
                    {
                        roleId = "security_professional",
                        namespacePrefix = $"/project/{ProjectId}/risks",
                        permission = "review"
                    },
                    new
                    {
                        roleId = "research_lead",
                        namespacePrefix = $"/project/{ProjectId}/role/research_lead/lens",
                        permission = "read"
                    }
                ],
            sourceDocuments = sourceDocuments ?? CreateSourceDocuments(),
            accessPreviewReportId = accessPreviewReportId ?? "preview-reg02-001",
            auditExportId = auditExportId ?? "audit-export-reg02-001",
            registrationNote = "Payload-safe registration test note."
        };
    }

    private static object[] CreateOwnerAssignments(params string[] omittedRoleIds)
    {
        var omitted = omittedRoleIds.ToHashSet(StringComparer.Ordinal);
        return new[]
        {
            new
            {
                principalId = ProductOwnerPrincipalId,
                roleId = "product_owner",
                projectAccessLevel = "reviewer",
                principalLabel = "product_owner"
            },
            new
            {
                principalId = KnowledgeStewardPrincipalId,
                roleId = "knowledge_steward",
                projectAccessLevel = "reviewer",
                principalLabel = "knowledge_steward"
            },
            new
            {
                principalId = SecurityPrincipalId,
                roleId = "security_professional",
                projectAccessLevel = "reviewer",
                principalLabel = "security_ops"
            },
            new
            {
                principalId = ResearchLeadPrincipalId,
                roleId = "research_lead",
                projectAccessLevel = "contributor",
                principalLabel = "research_lead"
            }
        }
        .Where(assignment => !omitted.Contains(assignment.roleId))
        .Cast<object>()
        .ToArray();
    }

    private static object[] CreateSourceDocuments()
    {
        return
        [
            new
            {
                path = "docs/project-registration-ux-plan.md",
                sourceContentSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                sourceOwnerRoleId = "product_owner"
            }
        ];
    }

    private static AdminProjectRegistrationCommand CreateRegistrationCommandWithMissingIdempotencyRecord()
    {
        return new AdminProjectRegistrationCommand(
            ActorPrincipalId,
            Guid.NewGuid(),
            "sha256:v2:" + new string('a', 64),
            OrgId,
            "Registered Memory Org",
            ProjectId,
            "Registered Memory Project",
            "active",
            [
                new AdminProjectRegistrationRoleDefinitionCommand(
                    "research_lead",
                    "Research Lead",
                    "Owns research interpretation during registration.",
                    "developer",
                    "active")
            ],
            [
                new AdminProjectRegistrationOwnerAssignmentCommand(
                    ProductOwnerPrincipalId,
                    "product_owner",
                    "reviewer",
                    "product_owner"),
                new AdminProjectRegistrationOwnerAssignmentCommand(
                    KnowledgeStewardPrincipalId,
                    "knowledge_steward",
                    "reviewer",
                    "knowledge_steward"),
                new AdminProjectRegistrationOwnerAssignmentCommand(
                    SecurityPrincipalId,
                    "security_professional",
                    "reviewer",
                    "security_ops"),
                new AdminProjectRegistrationOwnerAssignmentCommand(
                    ResearchLeadPrincipalId,
                    "research_lead",
                    "contributor",
                    "research_lead")
            ],
            [
                new AdminProjectRegistrationNamespaceGrantCommand(
                    null,
                    "product_owner",
                    $"/project/{ProjectId}/goals",
                    "write")
            ],
            [
                new AdminProjectRegistrationSourceDocumentCommand(
                    "docs/project-registration-ux-plan.md",
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    "product_owner")
            ],
            "preview-reg02-001",
            "audit-export-reg02-001",
            "Payload-safe registration test note.",
            "POST",
            "/api/admin/projects/register",
            "registration-idempotency-rollback-test");
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string postgresConnectionString,
        Guid? targetActorPrincipalId = null)
    {
        return MemorySystemApiTestFactory.Create(
            postgresConnectionString,
            TestApiKey,
            (targetActorPrincipalId ?? ActorPrincipalId).ToString());
    }

    private static HttpRequestMessage CreateAuthenticatedJsonRequest(string path, object body, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Api-Key", TestApiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return request;
    }

    private static async Task<string?> ReadProjectStatusAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT status
            FROM projects
            WHERE id = @project_id;
            """);
        command.Parameters.AddWithValue("project_id", ProjectId);

        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<bool> HasProjectRoleDefinitionAsync(NpgsqlDataSource dataSource, string roleId)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM project_role_definitions
                WHERE project_id = @project_id
                    AND role_id = @role_id
                    AND status = 'active'
            );
            """);
        command.Parameters.AddWithValue("project_id", ProjectId);
        command.Parameters.AddWithValue("role_id", roleId);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<bool> HasProjectMembershipAsync(
        NpgsqlDataSource dataSource,
        Guid principalId,
        string accessLevel)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM project_memberships
                WHERE project_id = @project_id
                    AND principal_id = @principal_id
                    AND access_level = @access_level
            );
            """);
        command.Parameters.AddWithValue("project_id", ProjectId);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("access_level", accessLevel);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<bool> HasRoleAssignmentAsync(
        NpgsqlDataSource dataSource,
        Guid principalId,
        string roleId)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM role_assignments
                WHERE principal_id = @principal_id
                    AND role_id = @role_id
                    AND scope_type = 'project'
                    AND scope_id = @project_id
            );
            """);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("project_id", ProjectId);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<bool> HasRoleNamespaceGrantAsync(
        NpgsqlDataSource dataSource,
        string roleId,
        string namespacePrefix,
        string permission)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM memory_access_grants
                WHERE role_id = @role_id
                    AND principal_id IS NULL
                    AND namespace_prefix = @namespace_prefix
                    AND permission = @permission
            );
            """);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("namespace_prefix", namespacePrefix);
        command.Parameters.AddWithValue("permission", permission);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<bool> HasProjectRootNamespaceGrantAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM memory_access_grants
                WHERE namespace_prefix = @namespace_prefix
            );
            """);
        command.Parameters.AddWithValue("namespace_prefix", $"/project/{ProjectId}");

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<bool> HasAdminNamespaceGrantAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM memory_access_grants
                WHERE permission = 'admin'
            );
            """);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<long> CountAuditEventsAsync(
        NpgsqlDataSource dataSource,
        string actionType,
        Guid? actorPrincipalId = null)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT count(*)
            FROM access_audit_events
            WHERE actor_principal_id = @actor_principal_id
                AND action_type = @action_type;
            """);
        command.Parameters.AddWithValue("actor_principal_id", actorPrincipalId ?? ActorPrincipalId);
        command.Parameters.AddWithValue("action_type", actionType);

        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Audit count was not returned."));
    }
}
