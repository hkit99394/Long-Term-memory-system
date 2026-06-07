using MemorySystem.Infrastructure.DomainMapping;

namespace MemorySystem.UnitTests;

public sealed class PostgresDomainMappingTests
{
    [Fact]
    public void Infrastructure_mapping_normalizes_database_boundary_values_through_domain()
    {
        var projectId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var sourceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");

        var scope = PostgresDomainMapping.RequireScope(" PROJECT ", projectId.ToString().ToUpperInvariant());
        var sourceEvidence = PostgresDomainMapping.RequireSourceEvidence(
            sourceEventId,
            " USER_SCOPED ",
            " PERSONAL ");

        Assert.Equal("project", scope.ScopeType);
        Assert.Equal(projectId.ToString(), scope.ScopeId);
        Assert.Equal($"/project/{projectId}/decisions", PostgresDomainMapping.RequireNamespace($"/project/{projectId}/decisions"));
        Assert.Equal("cto", PostgresDomainMapping.RequireRoleId(" CTO "));
        Assert.Equal("active", PostgresDomainMapping.RequireLifecycleStatus("active"));
        Assert.Equal("standard", PostgresDomainMapping.RequireRetentionClass(" STANDARD "));
        Assert.Equal("personal", PostgresDomainMapping.RequireSensitivity(" PERSONAL "));
        Assert.Equal("user_scoped", PostgresDomainMapping.RequireTrustLevel(" USER_SCOPED "));
        Assert.Equal("over_broad", PostgresDomainMapping.RequireFeedbackType("over-broad"));
        Assert.Equal(sourceEventId, sourceEvidence.SourceEventId);
        Assert.Equal("user_scoped", sourceEvidence.TrustLevel.Value);
        Assert.Equal("personal", sourceEvidence.Sensitivity.Value);
    }

    [Fact]
    public void Infrastructure_mapping_rejects_invalid_database_boundary_values()
    {
        Assert.Throws<InvalidOperationException>(() => PostgresDomainMapping.RequireScope("project", "not-a-guid"));
        Assert.Throws<InvalidOperationException>(() => PostgresDomainMapping.RequireNamespace("project/without/root"));
        Assert.Throws<InvalidOperationException>(() => PostgresDomainMapping.RequireRoleId("1intern"));
        Assert.Throws<InvalidOperationException>(() => PostgresDomainMapping.RequireLifecycleStatus("ACTIVE"));
        Assert.Throws<InvalidOperationException>(() => PostgresDomainMapping.RequireRetentionClass("forever"));
        Assert.Throws<InvalidOperationException>(() => PostgresDomainMapping.RequireSensitivity("classified"));
        Assert.Throws<InvalidOperationException>(() => PostgresDomainMapping.RequireTrustLevel("untrusted"));
        Assert.Throws<InvalidOperationException>(() => PostgresDomainMapping.RequireFeedbackType("maybe"));
    }
}
