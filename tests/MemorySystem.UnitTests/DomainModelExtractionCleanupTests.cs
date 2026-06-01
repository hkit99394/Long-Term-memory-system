using MemorySystem.Application.MemoryEvaluations;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Retrieval;
using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Scopes;

namespace MemorySystem.UnitTests;

public sealed class DomainModelExtractionCleanupTests
{
    [Fact]
    public void Dm06_keeps_application_policy_facades_backed_by_domain_vocabularies()
    {
        Assert.Equal(MemoryScopeType.All.Order(), MemoryScopePolicy.ScopeTypes.Order());
        Assert.Equal(MemoryRoleId.All.Order(), MemoryScopePolicy.RoleIds.Order());
        Assert.Equal(MemoryRetrievalFeedbackType.All.Order(), MemoryRetrievalFeedbackTypes.All.Order());

        var root = FindRepositoryRoot();
        var feedbackFacade = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MemorySystem.Application",
            "MemoryEvaluations",
            "MemoryRetrievalFeedbackTypes.cs"));
        var scopePolicy = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MemorySystem.Application",
            "Scopes",
            "MemoryScopePolicy.cs"));
        var eventScopePolicy = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MemorySystem.Application",
            "Scopes",
            "MemoryEventScopePolicy.cs"));
        var accessAuditStore = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MemorySystem.Infrastructure",
            "AccessAuditing",
            "PostgresAccessAuditEventStore.cs"));

        Assert.Contains("MemoryRetrievalFeedbackType.TryNormalize", feedbackFacade, StringComparison.Ordinal);
        Assert.DoesNotContain("new HashSet<string>", feedbackFacade, StringComparison.Ordinal);

        Assert.Contains("MemoryScopeId.TryNormalize", scopePolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("TryParseScopedGuid", scopePolicy, StringComparison.Ordinal);
        Assert.Contains("MemoryScopeId.TryNormalize", eventScopePolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("TryParseScopedGuid", eventScopePolicy, StringComparison.Ordinal);

        Assert.Contains("MemoryScopeType.TryNormalize", accessAuditStore, StringComparison.Ordinal);
        Assert.Contains("MemoryRoleId.TryNormalize", accessAuditStore, StringComparison.Ordinal);
        Assert.DoesNotContain("\"designer\"", accessAuditStore, StringComparison.Ordinal);
        Assert.DoesNotContain("\"developer\"", accessAuditStore, StringComparison.Ordinal);
        Assert.DoesNotContain("\"cto\"", accessAuditStore, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MemorySystem.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
