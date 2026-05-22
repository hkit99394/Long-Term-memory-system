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

    private static MemoryScopeResolution ProjectScope()
    {
        return new MemoryScopeResolution(
            "project",
            ProjectId.ToString(),
            OrgId: OrgId,
            ProjectId: ProjectId);
    }

    private sealed class FakeMemoryAccessReferenceStore : IMemoryAccessReferenceStore
    {
        public string? OrganizationAccessLevel { get; init; }
        public string? ProjectAccessLevel { get; init; }
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
