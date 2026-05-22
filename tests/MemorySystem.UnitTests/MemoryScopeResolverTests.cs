using MemorySystem.Application.Scopes;

namespace MemorySystem.UnitTests;

public sealed class MemoryScopeResolverTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid AgentPrincipalId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Theory]
    [InlineData("global", "global", "global", null)]
    [InlineData("org", "22222222-2222-4222-8222-222222222222", "22222222-2222-4222-8222-222222222222", null)]
    [InlineData("project", "33333333-3333-4333-8333-333333333333", "33333333-3333-4333-8333-333333333333", "22222222-2222-4222-8222-222222222222")]
    [InlineData("user", "11111111-1111-4111-8111-111111111111", "11111111-1111-4111-8111-111111111111", null)]
    [InlineData("agent", "44444444-4444-4444-8444-444444444444", "44444444-4444-4444-8444-444444444444", null)]
    [InlineData("role", "CTO", "cto", null)]
    [InlineData("session", "session-1", "session-1", null)]
    public async Task ResolveEventScopeAsync_resolves_supported_scopes(
        string scopeType,
        string scopeId,
        string expectedScopeId,
        string? expectedOrgId)
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveEventScopeAsync(new MemoryEventScopeRequest(
            PrincipalId,
            scopeType,
            scopeId,
            ScopeOrgId: null,
            ConversationId: null,
            AgentPrincipalId: null,
            RoleId: null));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Resolution);
        Assert.Equal(scopeType.ToLowerInvariant(), result.Resolution.ScopeType);
        Assert.Equal(expectedScopeId, result.Resolution.ScopeId);

        if (expectedOrgId is not null)
        {
            Assert.Equal(Guid.Parse(expectedOrgId), result.Resolution.OrgId);
        }
    }

    [Fact]
    public async Task ResolveEventScopeAsync_rejects_project_scope_with_mismatched_organization()
    {
        var resolver = CreateResolver();
        var wrongOrgId = Guid.Parse("99999999-9999-4999-8999-999999999999");

        var result = await resolver.ResolveEventScopeAsync(new MemoryEventScopeRequest(
            PrincipalId,
            "project",
            ProjectId.ToString(),
            wrongOrgId,
            ConversationId: null,
            AgentPrincipalId: null,
            RoleId: null));

        Assert.False(result.Succeeded);
        Assert.Contains("does not belong to organization", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveProposalScopeAsync_rejects_missing_agent_principal()
    {
        var resolver = CreateResolver(activeAgent: false);

        var result = await resolver.ResolveProposalScopeAsync(new MemoryProposalScopeRequest(
            PrincipalId,
            "agent",
            AgentPrincipalId.ToString(),
            $"/agent/{AgentPrincipalId}/private"));

        Assert.False(result.Succeeded);
        Assert.Contains("active agent principal", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveProposalScopeAsync_derives_project_organization()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveProposalScopeAsync(new MemoryProposalScopeRequest(
            PrincipalId,
            "project",
            ProjectId.ToString(),
            $"/project/{ProjectId}/decisions"));

        Assert.True(result.Succeeded);
        Assert.Equal(ProjectId.ToString(), result.Resolution?.ScopeId);
        Assert.Equal(ProjectId, result.Resolution?.ProjectId);
        Assert.Equal(OrgId, result.Resolution?.OrgId);
    }

    private static MemoryScopeResolver CreateResolver(bool activeAgent = true)
    {
        return new MemoryScopeResolver(new FakeMemoryScopeReferenceStore(activeAgent));
    }

    private sealed class FakeMemoryScopeReferenceStore(bool activeAgent) : IMemoryScopeReferenceStore
    {
        public Task<bool> OrganizationExistsAsync(Guid orgId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(orgId == OrgId);
        }

        public Task<ProjectScopeReference?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProjectScopeReference?>(
                projectId == ProjectId
                    ? new ProjectScopeReference(ProjectId, OrgId)
                    : null);
        }

        public Task<bool> PrincipalExistsAsync(
            Guid principalId,
            string? principalType = null,
            CancellationToken cancellationToken = default)
        {
            var exists = principalType switch
            {
                "agent" => activeAgent && principalId == AgentPrincipalId,
                _ => principalId == PrincipalId || principalId == AgentPrincipalId
            };

            return Task.FromResult(exists);
        }
    }
}
