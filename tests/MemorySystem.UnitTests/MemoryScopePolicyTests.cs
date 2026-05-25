using MemorySystem.Application.Scopes;

namespace MemorySystem.UnitTests;

public sealed class MemoryScopePolicyTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Theory]
    [InlineData("global", "global", "/global/policies", "global")]
    [InlineData("org", "22222222-2222-4222-8222-222222222222", "/org/22222222-2222-4222-8222-222222222222/preferences", "22222222-2222-4222-8222-222222222222")]
    [InlineData("project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/decisions", "33333333-3333-4333-8333-333333333333")]
    [InlineData("user", "11111111-1111-4111-8111-111111111111", "/user/11111111-1111-4111-8111-111111111111/preferences", "11111111-1111-4111-8111-111111111111")]
    [InlineData("agent", "44444444-4444-4444-8444-444444444444", "/agent/44444444-4444-4444-8444-444444444444/private", "44444444-4444-4444-8444-444444444444")]
    [InlineData("role", "CTO", "/role/cto/principles", "cto")]
    [InlineData("session", "session-1", "/session/session-1/instructions", "session-1")]
    public void TryNormalizeProposalScope_returns_normalized_scope_id(
        string scopeType,
        string scopeId,
        string namespaceValue,
        string expectedScopeId)
    {
        var result = MemoryScopePolicy.TryNormalizeProposalScope(
            PrincipalId,
            scopeType,
            scopeId,
            namespaceValue,
            out var normalizedScopeId,
            out var error);

        Assert.True(result);
        Assert.Equal(expectedScopeId, normalizedScopeId);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("user", "22222222-2222-4222-8222-222222222222", "/user/22222222-2222-4222-8222-222222222222/preferences", "authenticated principal")]
    [InlineData("user", "not-a-guid", "/user/not-a-guid/preferences", "valid GUID")]
    [InlineData("user", "11111111-1111-4111-8111-111111111111", "/USER/11111111-1111-4111-8111-111111111111/preferences", "namespace scope type")]
    [InlineData("org", "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "/org/AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA/preferences", "namespace must start")]
    [InlineData("role", "intern", "/role/intern/principles", "supported role")]
    [InlineData("role", "CTO", "/role/CTO/principles", "namespace must start")]
    [InlineData("session", "global", "/session/global/instructions", "must not be 'global'")]
    [InlineData("project", "33333333-3333-4333-8333-333333333333", "/user/11111111-1111-4111-8111-111111111111/preferences", "namespace must start")]
    [InlineData("session", "session-1", "/session/session-10/instructions", "namespace must start")]
    [InlineData("project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/role/intern/lens", "not supported")]
    public void TryNormalizeProposalScope_rejects_invalid_scope(
        string scopeType,
        string scopeId,
        string namespaceValue,
        string expectedError)
    {
        var result = MemoryScopePolicy.TryNormalizeProposalScope(
            PrincipalId,
            scopeType,
            scopeId,
            namespaceValue,
            out var normalizedScopeId,
            out var error);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedScopeId);
        Assert.Contains(expectedError, error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" PROJECT ", "33333333-3333-4333-8333-333333333333", "project", "33333333-3333-4333-8333-333333333333")]
    [InlineData("role", "CTO", "role", "cto")]
    [InlineData("global", "GLOBAL", "global", "global")]
    [InlineData("session", "session-1", "session", "session-1")]
    public void TryNormalizeTargetScope_canonicalizes_read_scope(
        string scopeType,
        string scopeId,
        string expectedScopeType,
        string expectedScopeId)
    {
        var result = MemoryScopePolicy.TryNormalizeTargetScope(
            scopeType,
            scopeId,
            out var normalizedScopeType,
            out var normalizedScopeId,
            out var error);

        Assert.True(result);
        Assert.Equal(expectedScopeType, normalizedScopeType);
        Assert.Equal(expectedScopeId, normalizedScopeId);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("project", "not-a-guid", "valid GUID")]
    [InlineData("global", "not-global", "must be 'global'")]
    [InlineData("role", "intern", "supported role")]
    [InlineData("session", "global", "must not be 'global'")]
    public void TryNormalizeTargetScope_rejects_invalid_read_scope(
        string scopeType,
        string scopeId,
        string expectedError)
    {
        var result = MemoryScopePolicy.TryNormalizeTargetScope(
            scopeType,
            scopeId,
            out var normalizedScopeType,
            out var normalizedScopeId,
            out var error);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedScopeId);
        Assert.False(string.IsNullOrWhiteSpace(normalizedScopeType));
        Assert.Contains(expectedError, error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CTO", "cto")]
    [InlineData(" cfo ", "cfo")]
    [InlineData(null, null)]
    public void TryNormalizeRoleId_canonicalizes_optional_role_id(
        string? roleId,
        string? expectedRoleId)
    {
        var result = MemoryScopePolicy.TryNormalizeRoleId(roleId, out var normalizedRoleId, out var error);

        Assert.True(result);
        Assert.Equal(expectedRoleId, normalizedRoleId);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("global", null, "global", null)]
    [InlineData("org", "22222222-2222-4222-8222-222222222222", "22222222-2222-4222-8222-222222222222", null)]
    [InlineData("user", "11111111-1111-4111-8111-111111111111", "11111111-1111-4111-8111-111111111111", null)]
    [InlineData("role", "CTO", "cto", "cto")]
    [InlineData("session", "session-1", "session-1", null)]
    public void TryNormalizeEventScope_returns_scope_resolution(
        string scopeType,
        string? scopeId,
        string expectedScopeId,
        string? expectedRoleId)
    {
        var result = MemoryEventScopePolicy.TryNormalizeEventScope(
            PrincipalId,
            scopeType,
            scopeId,
            scopeOrgId: null,
            conversationId: null,
            agentPrincipalId: null,
            roleId: null,
            out var resolution,
            out var error);

        Assert.True(result);
        Assert.Equal(scopeType, resolution.ScopeType);
        Assert.Equal(expectedScopeId, resolution.ScopeId);
        Assert.Equal(expectedRoleId, resolution.RoleId);
        Assert.Equal(expectedRoleId, resolution.ScopeRoleId);
        Assert.Null(error);
    }

    [Fact]
    public void TryNormalizeEventScope_rejects_actor_role_outside_role_scope()
    {
        var result = MemoryEventScopePolicy.TryNormalizeEventScope(
            PrincipalId,
            "user",
            PrincipalId.ToString(),
            scopeOrgId: null,
            conversationId: null,
            agentPrincipalId: null,
            roleId: "CTO",
            out var resolution,
            out var error);

        Assert.False(result);
        Assert.Null(resolution);
        Assert.Contains("role-scoped events", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryNormalizeEventScope_rejects_actor_agent_outside_agent_scope()
    {
        var result = MemoryEventScopePolicy.TryNormalizeEventScope(
            PrincipalId,
            "user",
            PrincipalId.ToString(),
            scopeOrgId: null,
            conversationId: null,
            agentPrincipalId: Guid.Parse("44444444-4444-4444-8444-444444444444"),
            roleId: null,
            out var resolution,
            out var error);

        Assert.False(result);
        Assert.Null(resolution);
        Assert.Contains("agent-scoped events", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryNormalizeEventScope_sets_agent_principal_from_agent_scope()
    {
        var agentPrincipalId = Guid.Parse("44444444-4444-4444-8444-444444444444");

        var result = MemoryEventScopePolicy.TryNormalizeEventScope(
            PrincipalId,
            "agent",
            agentPrincipalId.ToString(),
            scopeOrgId: null,
            conversationId: null,
            agentPrincipalId: null,
            roleId: null,
            out var resolution,
            out var error);

        Assert.True(result);
        Assert.Equal(agentPrincipalId.ToString(), resolution.ScopeId);
        Assert.Equal(agentPrincipalId, resolution.PrincipalId);
        Assert.Equal(agentPrincipalId, resolution.AgentPrincipalId);
        Assert.Null(error);
    }

    [Fact]
    public void TryNormalizeEventScope_derives_conversation_from_guid_session_scope()
    {
        var conversationId = Guid.Parse("55555555-5555-4555-8555-555555555555");

        var result = MemoryEventScopePolicy.TryNormalizeEventScope(
            PrincipalId,
            "session",
            conversationId.ToString(),
            scopeOrgId: null,
            conversationId: null,
            agentPrincipalId: null,
            roleId: null,
            out var resolution,
            out var error);

        Assert.True(result);
        Assert.Equal(conversationId.ToString(), resolution.ScopeId);
        Assert.Equal(conversationId, resolution.ConversationId);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("user", "22222222-2222-4222-8222-222222222222", "authenticated principal")]
    [InlineData("agent", "not-a-guid", "valid GUID")]
    [InlineData("role", "intern", "supported role")]
    [InlineData("session", "global", "must not be 'global'")]
    [InlineData("unknown", "global", "scopeType is not supported")]
    public void TryNormalizeEventScope_rejects_invalid_scope(
        string scopeType,
        string scopeId,
        string expectedError)
    {
        var result = MemoryEventScopePolicy.TryNormalizeEventScope(
            PrincipalId,
            scopeType,
            scopeId,
            scopeOrgId: null,
            conversationId: null,
            agentPrincipalId: null,
            roleId: null,
            out var resolution,
            out var error);

        Assert.False(result);
        Assert.Null(resolution);
        Assert.Contains(expectedError, error, StringComparison.Ordinal);
    }
}
