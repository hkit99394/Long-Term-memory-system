using MemorySystem.Application.Access;
using MemorySystem.Application.Scopes;

namespace MemorySystem.UnitTests;

public sealed class MemoryAccessAuthorizerTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OtherPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OrgId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public async Task AuthorizeAsync_allows_project_write_with_membership_and_namespace_grant()
    {
        var store = new FakeMemoryAccessReferenceStore
        {
            ProjectAccessLevel = "contributor",
            HasGrant = true
        };
        var authorizer = new MemoryAccessAuthorizer(store);

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Write,
            ProjectScope(),
            $"/project/{ProjectId}/decisions"));

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_denies_project_write_without_membership()
    {
        var store = new FakeMemoryAccessReferenceStore
        {
            HasGrant = true
        };
        var authorizer = new MemoryAccessAuthorizer(store);

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Write,
            ProjectScope(),
            $"/project/{ProjectId}/decisions"));

        Assert.False(decision.Allowed);
        Assert.Contains("membership access to project", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeAsync_denies_project_write_without_namespace_grant()
    {
        var store = new FakeMemoryAccessReferenceStore
        {
            ProjectAccessLevel = "admin"
        };
        var authorizer = new MemoryAccessAuthorizer(store);

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Write,
            ProjectScope(),
            $"/project/{ProjectId}/decisions"));

        Assert.False(decision.Allowed);
        Assert.Contains("does not have write access to namespace", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeAsync_allows_project_review_through_active_project_org_admin()
    {
        var store = new FakeMemoryAccessReferenceStore
        {
            OrganizationAccessLevel = "admin",
            HasGrant = true
        };
        var authorizer = new MemoryAccessAuthorizer(store);

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Review,
            ProjectScope(),
            $"/project/{ProjectId}/decisions"));

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_denies_project_review_through_org_admin_when_project_is_inactive()
    {
        var store = new FakeMemoryAccessReferenceStore
        {
            ActiveProjectOrgId = null,
            OrganizationAccessLevel = "admin",
            HasGrant = true
        };
        var authorizer = new MemoryAccessAuthorizer(store);

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Review,
            ProjectScope(),
            $"/project/{ProjectId}/decisions"));

        Assert.False(decision.Allowed);
        Assert.Contains("not active", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeAsync_denies_role_namespace_without_matching_role_assignment()
    {
        var store = new FakeMemoryAccessReferenceStore
        {
            ProjectAccessLevel = "reader",
            HasGrant = true
        };
        var authorizer = new MemoryAccessAuthorizer(store);

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Read,
            ProjectScope(),
            $"/project/{ProjectId}/role/cto/lens"));

        Assert.False(decision.Allowed);
        Assert.Contains("does not have role 'cto'", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeAsync_denies_org_role_namespace_without_matching_role_assignment()
    {
        var store = new FakeMemoryAccessReferenceStore
        {
            OrganizationAccessLevel = "reader",
            HasGrant = true
        };
        var authorizer = new MemoryAccessAuthorizer(store);

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Read,
            OrganizationScope(),
            $"/org/{OrgId}/role/cto/lens"));

        Assert.False(decision.Allowed);
        Assert.Contains("does not have role 'cto'", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeAsync_allows_role_scope_when_principal_has_role_assignment()
    {
        var store = new FakeMemoryAccessReferenceStore
        {
            HasRoleAssignment = true
        };
        var authorizer = new MemoryAccessAuthorizer(store);

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Read,
            new MemoryScopeResolution(
                "role",
                "cto",
                RoleId: "cto",
                ScopeRoleId: "cto")));

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_denies_cross_user_scope()
    {
        var authorizer = new MemoryAccessAuthorizer(new FakeMemoryAccessReferenceStore());

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Write,
            new MemoryScopeResolution(
                "user",
                OtherPrincipalId.ToString(),
                PrincipalId: OtherPrincipalId),
            $"/user/{OtherPrincipalId}/preferences"));

        Assert.False(decision.Allowed);
        Assert.Contains("authenticated principal", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeAsync_denies_session_read_until_session_ownership_is_modeled()
    {
        var authorizer = new MemoryAccessAuthorizer(new FakeMemoryAccessReferenceStore
        {
            HasGrant = true
        });

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Read,
            new MemoryScopeResolution("session", "session-1"),
            "/session/session-1/instructions"));

        Assert.False(decision.Allowed);
        Assert.Contains("session ownership", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeAsync_allows_session_write_to_continue_using_namespace_grants_for_event_append()
    {
        var authorizer = new MemoryAccessAuthorizer(new FakeMemoryAccessReferenceStore
        {
            HasGrant = true
        });

        var decision = await authorizer.AuthorizeAsync(new MemoryAccessRequest(
            PrincipalId,
            MemoryAccessPermissions.Write,
            new MemoryScopeResolution("session", "session-1"),
            "/session/session-1/events"));

        Assert.True(decision.Allowed);
    }

    private static MemoryScopeResolution ProjectScope()
    {
        return new MemoryScopeResolution(
            "project",
            ProjectId.ToString(),
            OrgId: OrgId,
            ProjectId: ProjectId);
    }

    private static MemoryScopeResolution OrganizationScope()
    {
        return new MemoryScopeResolution(
            "org",
            OrgId.ToString(),
            OrgId: OrgId);
    }

    private sealed class FakeMemoryAccessReferenceStore : IMemoryAccessReferenceStore
    {
        public string? OrganizationAccessLevel { get; init; }
        public string? ProjectAccessLevel { get; init; }
        public Guid? ActiveProjectOrgId { get; init; } = OrgId;
        public bool HasRoleAssignment { get; init; }
        public bool HasGrant { get; init; }

        public Task<string?> FindOrganizationAccessLevelAsync(
            Guid principalId,
            Guid orgId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OrganizationAccessLevel);
        }

        public Task<string?> FindProjectAccessLevelAsync(
            Guid principalId,
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProjectAccessLevel);
        }

        public Task<Guid?> FindActiveProjectOrganizationIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ActiveProjectOrgId);
        }

        public Task<bool> HasRoleAssignmentAsync(
            Guid principalId,
            string roleId,
            MemoryScopeResolution scope,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HasRoleAssignment);
        }

        public Task<bool> HasNamespaceGrantAsync(
            Guid principalId,
            MemoryScopeResolution scope,
            string permission,
            string namespaceValue,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HasGrant);
        }
    }
}
