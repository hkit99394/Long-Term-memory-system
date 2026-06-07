namespace MemorySystem.UnitTests;

public sealed class CanonicalMemoryTypesIp06Tests
{
    [Fact]
    public void Ip06_canonical_memory_types_are_documented_and_exposed()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "canonical-memory-types-ip06.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var openApi = File.ReadAllText(Path.Combine(root, "docs", "api", "agent-memory-v1.openapi.json"));
        var memoryType = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Domain", "MemoryTypes", "MemoryType.cs"));
        var proposalWorkflow = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "MemoryProposals", "MemoryProposalWorkflow.cs"));
        var findingService = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "MemoryFacts", "MemoryFactFindingService.cs"));

        Assert.Contains("| IP-06 | P0 | Done | Knowledge Steward + Developer | Canonical Memory Types |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Canonical Memory Types IP-06", contract, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Domain.MemoryTypes.MemoryType", contract, StringComparison.Ordinal);
        Assert.Contains("[Canonical Memory Types IP-06](canonical-memory-types-ip06.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("canonical-memory-types-ip06.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("TryNormalizeProposalType", proposalWorkflow, StringComparison.Ordinal);
        Assert.Contains("TryNormalizeQueryableType", findingService, StringComparison.Ordinal);

        foreach (var canonicalType in new[]
        {
            "goal",
            "target",
            "fact",
            "decision",
            "rationale",
            "risk",
            "assumption",
            "constraint",
            "requirement",
            "release_evidence",
            "role_lens"
        })
        {
            Assert.Contains($"`{canonicalType}`", contract, StringComparison.Ordinal);
            Assert.Contains($"\"{canonicalType}\"", openApi, StringComparison.Ordinal);
            Assert.Contains(ToPascalCaseIdentifier(canonicalType), memoryType, StringComparison.Ordinal);
        }
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

    private static string ToPascalCaseIdentifier(string value)
    {
        return string.Concat(
            value.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
