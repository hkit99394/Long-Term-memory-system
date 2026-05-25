using MemorySystem.Application.Scopes;

namespace MemorySystem.UnitTests;

public sealed class MemoryNamespaceParserTests
{
    [Theory]
    [InlineData("/global/instructions", "global", "global", null)]
    [InlineData("/org/22222222-2222-4222-8222-222222222222/policies", "org", "22222222-2222-4222-8222-222222222222", null)]
    [InlineData("/user/11111111-1111-4111-8111-111111111111/preferences", "user", "11111111-1111-4111-8111-111111111111", null)]
    [InlineData("/project/33333333-3333-4333-8333-333333333333/decisions", "project", "33333333-3333-4333-8333-333333333333", null)]
    [InlineData("/project/33333333-3333-4333-8333-333333333333/role/CTO/lens", "project", "33333333-3333-4333-8333-333333333333", "cto")]
    [InlineData("/org/22222222-2222-4222-8222-222222222222/role/CTO/lens", "org", "22222222-2222-4222-8222-222222222222", "cto")]
    [InlineData("/role/CTO/shared", "role", "cto", "cto")]
    [InlineData("/agent/44444444-4444-4444-8444-444444444444/private", "agent", "44444444-4444-4444-8444-444444444444", null)]
    [InlineData("/session/session%1/working_memory", "session", "session%1", null)]
    public void TryParse_returns_canonical_scope_metadata(
        string namespaceValue,
        string expectedScopeType,
        string expectedScopeId,
        string? expectedRoleId)
    {
        var result = MemoryNamespaceParser.TryParse(namespaceValue, out var memoryNamespace, out var error);

        Assert.True(result);
        Assert.Equal(expectedScopeType, memoryNamespace.ScopeType);
        Assert.Equal(expectedScopeId, memoryNamespace.ScopeId);
        Assert.Equal(expectedRoleId, memoryNamespace.RoleId);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("", "must start")]
    [InlineData("global/instructions", "must start")]
    [InlineData("/global", "category")]
    [InlineData("/global/", "empty path")]
    [InlineData("/project/not-a-guid/decisions", "valid GUID")]
    [InlineData("/org/22222222-2222-4222-8222-222222222222/role/cto", "category")]
    [InlineData("/project/33333333-3333-4333-8333-333333333333/role/intern/lens", "not supported")]
    [InlineData("/project/33333333-3333-4333-8333-333333333333/role/cto", "category")]
    [InlineData("/role/intern/shared", "not supported")]
    [InlineData("/session/global/working_memory", "must not be 'global'")]
    [InlineData("/session/session-1/../working_memory", "relative path")]
    public void TryParse_rejects_malformed_namespaces(
        string namespaceValue,
        string expectedError)
    {
        var result = MemoryNamespaceParser.TryParse(namespaceValue, out var memoryNamespace, out var error);

        Assert.False(result);
        Assert.Null(memoryNamespace);
        Assert.Contains(expectedError, error, StringComparison.Ordinal);
    }
}
