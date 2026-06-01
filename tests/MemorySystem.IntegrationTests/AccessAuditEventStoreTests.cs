using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Authentication;
using MemorySystem.Infrastructure.AccessAuditing;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class AccessAuditEventStoreTests
{
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid TargetPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ServicePrincipalId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task RecordAsync_writes_payload_safe_records_for_all_enterprise_access_categories()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_access_audit_store_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, ActorPrincipalId);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, TargetPrincipalId);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(
                databaseConnectionString,
                ServicePrincipalId,
                principalType: "service",
                displayName: "Pilot Service");
            await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(databaseConnectionString, OrgId, ProjectId);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresAccessAuditEventStore(dataSource);

            var records = new[]
            {
                await store.RecordAsync(new AccessAuditEventCommand(
                    AccessAuditActionTypes.Authentication,
                    AccessAuditOutcomes.Succeeded,
                    ActorPrincipalId: ActorPrincipalId,
                    PrincipalType: "human",
                    AuthMethod: AuthenticationMethods.ApiKey,
                    CredentialId: "test-key",
                    RequestMethod: "get",
                    RequestPath: "/api/memory/context",
                    CorrelationId: "corr-auth-success",
                    Metadata: new Dictionary<string, string?> { ["scheme"] = "api_key" })),
                await store.RecordAsync(new AccessAuditEventCommand(
                    AccessAuditActionTypes.Authentication,
                    AccessAuditOutcomes.Failed,
                    AuthMethod: AuthenticationMethods.Oidc,
                    ReasonCode: "unbound_subject",
                    RequestMethod: "POST",
                    RequestPath: "/api/memory/context",
                    Metadata: new Dictionary<string, string?> { ["issuer"] = "https://issuer.example.test" })),
                await store.RecordAsync(new AccessAuditEventCommand(
                    AccessAuditActionTypes.AuthorizationDenied,
                    AccessAuditOutcomes.Denied,
                    ActorPrincipalId: ActorPrincipalId,
                    ScopeType: "project",
                    ScopeId: ProjectId.ToString(),
                    NamespacePrefix: $"/project/{ProjectId}/memory",
                    Permission: MemoryAccessPermissions.Read,
                    ResourceType: "memory_fact",
                    ResourceId: Guid.NewGuid().ToString(),
                    ReasonCode: "missing_namespace_grant")),
                await store.RecordAsync(new AccessAuditEventCommand(
                    AccessAuditActionTypes.OrganizationMembershipChange,
                    AccessAuditOutcomes.Succeeded,
                    ActorPrincipalId: ActorPrincipalId,
                    TargetPrincipalId: TargetPrincipalId,
                    ScopeType: "org",
                    ScopeId: OrgId.ToString(),
                    ResourceType: "organization_membership",
                    ResourceId: $"{OrgId}:{TargetPrincipalId}",
                    Metadata: new Dictionary<string, string?> { ["operation"] = "created" })),
                await store.RecordAsync(new AccessAuditEventCommand(
                    AccessAuditActionTypes.ProjectMembershipChange,
                    AccessAuditOutcomes.Succeeded,
                    ActorPrincipalId: ActorPrincipalId,
                    TargetPrincipalId: TargetPrincipalId,
                    ScopeType: "project",
                    ScopeId: ProjectId.ToString(),
                    ResourceType: "project_membership",
                    ResourceId: $"{ProjectId}:{TargetPrincipalId}",
                    Metadata: new Dictionary<string, string?> { ["accessLevel"] = "reviewer" })),
                await store.RecordAsync(new AccessAuditEventCommand(
                    AccessAuditActionTypes.RoleAssignmentChange,
                    AccessAuditOutcomes.Succeeded,
                    ActorPrincipalId: ActorPrincipalId,
                    TargetPrincipalId: TargetPrincipalId,
                    ScopeType: "project",
                    ScopeId: ProjectId.ToString(),
                    RoleId: "cto",
                    ResourceType: "role_assignment",
                    ResourceId: Guid.NewGuid().ToString())),
                await store.RecordAsync(new AccessAuditEventCommand(
                    AccessAuditActionTypes.NamespaceGrantChange,
                    AccessAuditOutcomes.Succeeded,
                    ActorPrincipalId: ActorPrincipalId,
                    TargetPrincipalId: TargetPrincipalId,
                    NamespacePrefix: $"/project/{ProjectId}/memory",
                    Permission: MemoryAccessPermissions.Admin,
                    ResourceType: "memory_access_grant",
                    ResourceId: Guid.NewGuid().ToString())),
                await store.RecordAsync(new AccessAuditEventCommand(
                    AccessAuditActionTypes.ServiceCredentialChange,
                    AccessAuditOutcomes.Succeeded,
                    ActorPrincipalId: ActorPrincipalId,
                    TargetPrincipalId: ServicePrincipalId,
                    PrincipalType: "service",
                    AuthMethod: AuthenticationMethods.ServiceAccount,
                    CredentialId: "svc-credential-1",
                    ResourceType: "service_credential",
                    ResourceId: "svc-credential-1",
                    Metadata: new Dictionary<string, string?> { ["operation"] = "rotated" })),
                await store.RecordAsync(new AccessAuditEventCommand(
                    AccessAuditActionTypes.AuditExport,
                    AccessAuditOutcomes.Succeeded,
                    ActorPrincipalId: ActorPrincipalId,
                    ResourceType: "audit_export",
                    ResourceId: Guid.NewGuid().ToString(),
                    Metadata: new Dictionary<string, string?> { ["format"] = "ndjson" }))
            };

            Assert.All(records, record => Assert.NotEqual(Guid.Empty, record.Id));
            Assert.All(records, record => Assert.NotEqual(default, record.OccurredAt));
            Assert.Equal("GET", records[0].RequestMethod);
            Assert.Equal("api_key", records[0].AuthMethod);
            Assert.Equal("test-key", records[0].CredentialId);
            Assert.Equal("missing_namespace_grant", records[2].ReasonCode);
            Assert.Equal(MemoryAccessPermissions.Admin, records[6].Permission);
            Assert.Equal("service_account", records[7].AuthMethod);

            await using var countCommand = dataSource.CreateCommand(
                """
                SELECT count(*)
                FROM access_audit_events
                WHERE action_type = ANY(@action_types);
                """);
            countCommand.Parameters.AddWithValue("action_types", AccessAuditActionTypes.All.ToArray());

            Assert.Equal(9L, await countCommand.ExecuteScalarAsync());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task RecordAsync_rejects_metadata_that_looks_like_raw_payload()
    {
        await using var dataSource = NpgsqlDataSource.Create(
            "Host=unused;Database=unused;Username=unused;Password=unused");
        var store = new PostgresAccessAuditEventStore(dataSource);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => store.RecordAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.AuditExport,
                AccessAuditOutcomes.Succeeded,
                Metadata: new Dictionary<string, string?> { ["rawPayload"] = "do not store this" })));

        Assert.Contains("raw payload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
