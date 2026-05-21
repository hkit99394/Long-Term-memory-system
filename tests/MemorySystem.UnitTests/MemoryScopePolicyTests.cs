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
    [InlineData("role", "intern", "/role/intern/principles", "supported role")]
    [InlineData("session", "global", "/session/global/instructions", "must not be 'global'")]
    [InlineData("project", "33333333-3333-4333-8333-333333333333", "/user/11111111-1111-4111-8111-111111111111/preferences", "namespace must start")]
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
}
