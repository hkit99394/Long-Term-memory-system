using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ReleaseReadinessStatusContractP2Tests
{
    [Fact]
    public void External_pilot_readiness_status_contract_is_machine_readable_and_linked()
    {
        var root = FindRepositoryRoot();
        var statusPath = Path.Combine(root, "docs", "external-pilot-readiness-status.json");
        var schemaPath = Path.Combine(root, "docs", "external-pilot-readiness-status.schema.json");
        var contract = File.ReadAllText(Path.Combine(root, "docs", "release-readiness-status-contract-p2.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var runbook = File.ReadAllText(Path.Combine(root, "docs", "target-environment-pilot-rehearsal-p0.md"));
        var goNoGo = File.ReadAllText(Path.Combine(root, "docs", "external-pilot-go-no-go-epr04-2026-06-04.md"));
        var releaseChecklist = File.ReadAllText(Path.Combine(root, "docs", "production-release-checklists-pi07.md"));

        Assert.True(File.Exists(statusPath));
        Assert.True(File.Exists(schemaPath));

        var schema = File.ReadAllText(schemaPath);
        Assert.Contains("\"External Pilot Readiness Status\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"no_go\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"blocksExternalInvite\"", schema, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(File.ReadAllText(statusPath));
        var status = document.RootElement;

        Assert.Equal("./external-pilot-readiness-status.schema.json", status.GetProperty("$schema").GetString());
        Assert.Equal(1, status.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("external-pilot", status.GetProperty("scope").GetString());

        var decision = status.GetProperty("decision");
        Assert.Equal("no_go", decision.GetProperty("status").GetString());
        Assert.False(decision.GetProperty("externalInviteApproved").GetBoolean());
        Assert.Equal(
            "docs/external-pilot-go-no-go-epr04-2026-06-04.md",
            decision.GetProperty("record").GetString());

        var gates = status.GetProperty("gates")
            .EnumerateArray()
            .ToDictionary(
                gate => gate.GetProperty("id").GetString()!,
                gate => gate,
                StringComparer.Ordinal);

        Assert.Equal("done", gates["EPR-01"].GetProperty("status").GetString());
        Assert.Equal("done", gates["EPR-02"].GetProperty("status").GetString());
        Assert.Equal("done", gates["EPR-03"].GetProperty("status").GetString());
        Assert.Equal("blocked", gates["EPR-04"].GetProperty("status").GetString());
        Assert.True(gates["EPR-04"].GetProperty("blocksExternalInvite").GetBoolean());
        Assert.Equal("done", gates["EPR-05"].GetProperty("status").GetString());
        Assert.Equal("done", gates["EPR-06"].GetProperty("status").GetString());

        var missingInputs = gates["EPR-04"]
            .GetProperty("missingInputs")
            .EnumerateArray()
            .Select(input => input.GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("controlled evidence prefix", missingInputs);
        Assert.Contains("real alert receiver acknowledgement", missingInputs);
        Assert.Contains("release owner signature", missingInputs);
        Assert.Contains("rollback owner signature", missingInputs);

        var requiredToGo = status.GetProperty("requiredToFlipToGo")
            .EnumerateArray()
            .Select(input => input.GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(
            "release owner and rollback owner signatures",
            requiredToGo);

        Assert.Contains("# Release Readiness Status Contract P2", contract, StringComparison.Ordinal);
        Assert.Contains("docs/external-pilot-readiness-status.json", contract, StringComparison.Ordinal);
        Assert.Contains("| EPR-06 | P2 | Done | Extract release-readiness status contract.", backlog, StringComparison.Ordinal);
        Assert.Contains("[Release Readiness Status Contract P2](release-readiness-status-contract-p2.md)", index, StringComparison.Ordinal);
        Assert.Contains("external-pilot-readiness-status.json", folderStructure, StringComparison.Ordinal);
        Assert.Contains("external-pilot-readiness-status.json", runbook, StringComparison.Ordinal);
        Assert.Contains("P2 release-readiness status contract completed", goNoGo, StringComparison.Ordinal);
        Assert.Contains("external-pilot-readiness-status.json", releaseChecklist, StringComparison.Ordinal);
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
