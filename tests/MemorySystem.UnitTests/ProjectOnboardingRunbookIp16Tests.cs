using System.Diagnostics;
using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ProjectOnboardingRunbookIp16Tests
{
    [Fact]
    public void Ip16_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "project-onboarding-runbook-ip16.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var runbook = File.ReadAllText(Path.Combine(root, "docs", "project-memory-runbook.md"));
        var boundary = File.ReadAllText(Path.Combine(root, "docs", "project-memory-boundary.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "project-onboarding-runbook.sh"));

        Assert.Contains("| IP-16 | P2 | Done | Product Owner + Knowledge Steward | Project Onboarding Runbook |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Project Onboarding Runbook IP-16", contract, StringComparison.Ordinal);
        Assert.Contains("[Project Onboarding Runbook IP-16](project-onboarding-runbook-ip16.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/project-onboarding-runbook-ip16.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("scripts/project-onboarding-runbook.sh", runbook, StringComparison.Ordinal);
        Assert.Contains("scripts/project-onboarding-runbook.sh", boundary, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/project-onboarding-runbook.sh", testing, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "/api/events",
            "/api/memory/proposals",
            "/api/memory/context",
            "/api/memory/context/feedback",
            "sourceContentSha256",
            "reviewCadence",
            "namespaceGrants",
            "role_lens"
        })
        {
            Assert.Contains(required, contract, StringComparison.Ordinal);
            Assert.Contains(required, script, StringComparison.Ordinal);
        }

        foreach (var memoryType in ExpectedMemoryTypes)
        {
            Assert.Contains(memoryType, contract, StringComparison.Ordinal);
            Assert.Contains(memoryType, script, StringComparison.Ordinal);
        }

        foreach (var roleId in ExpectedRoleIds)
        {
            Assert.Contains(roleId, contract, StringComparison.Ordinal);
            Assert.Contains(roleId, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Project_onboarding_dry_run_is_payload_safe_and_complete()
    {
        var root = FindRepositoryRoot();

        var syntax = await RunScriptAsync(root, ["-n", "scripts/project-onboarding-runbook.sh"]);
        Assert.Equal(0, syntax.ExitCode);

        var help = await RunScriptAsync(root, ["scripts/project-onboarding-runbook.sh", "--help"]);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("Build the IP-16 payload-safe project onboarding plan", help.StandardOutput, StringComparison.Ordinal);

        var dryRun = await RunScriptAsync(root, ["scripts/project-onboarding-runbook.sh", "--dry-run"]);
        Assert.Equal(0, dryRun.ExitCode);

        using var document = JsonDocument.Parse(dryRun.StandardOutput);
        var rootElement = document.RootElement;

        Assert.Equal("dry_run", rootElement.GetProperty("status").GetString());
        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.False(rootElement.GetProperty("liveWritesMemory").GetBoolean());
        Assert.Equal("project", rootElement.GetProperty("targetScope").GetProperty("scopeType").GetString());
        Assert.Equal(
            "9f8e7d6c-5b4a-4321-9123-abcdef123002",
            rootElement.GetProperty("targetScope").GetProperty("scopeId").GetString());

        var setupFlow = rootElement.GetProperty("setupFlow").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("define_project_scope", setupFlow);
        Assert.Contains("confirm_role_owners", setupFlow);
        Assert.Contains("grant_project_namespaces", setupFlow);
        Assert.Contains("confirm_canonical_memory_types", setupFlow);
        Assert.Contains("curate_seed_documents", setupFlow);
        Assert.Contains("append_source_evidence", setupFlow);
        Assert.Contains("schedule_review_cadence", setupFlow);

        var roles = rootElement.GetProperty("roleDefinitions").EnumerateArray().ToArray();
        Assert.Equal(ExpectedRoleIds.Length, roles.Length);
        foreach (var roleId in ExpectedRoleIds)
        {
            Assert.Contains(roles, role => role.GetProperty("roleId").GetString() == roleId);
        }

        var memoryTypes = rootElement.GetProperty("memoryTypes").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        foreach (var memoryType in ExpectedMemoryTypes)
        {
            Assert.Contains(memoryType, memoryTypes);
        }

        var namespaceGrants = rootElement.GetProperty("namespaceGrants").EnumerateArray().ToArray();
        Assert.True(namespaceGrants.Length >= ExpectedRoleIds.Length + 6);
        Assert.Contains(namespaceGrants, grant =>
            grant.GetProperty("namespace").GetString() == "/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/goals");
        Assert.Contains(namespaceGrants, grant =>
            grant.GetProperty("namespace").GetString() == "/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/release-evidence");
        foreach (var roleId in ExpectedRoleIds)
        {
            Assert.Contains(namespaceGrants, grant =>
                grant.GetProperty("namespace").GetString() ==
                $"/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/{roleId}/lens");
        }

        var seedDocuments = rootElement.GetProperty("seedDocuments").EnumerateArray().ToArray();
        Assert.True(seedDocuments.Length >= 8);
        Assert.All(seedDocuments, seed =>
        {
            Assert.StartsWith("docs/", seed.GetProperty("path").GetString(), StringComparison.Ordinal);
            Assert.True(seed.GetProperty("exists").GetBoolean());
            Assert.Equal(64, seed.GetProperty("sha256").GetString()!.Length);
            Assert.True(seed.GetProperty("suggestedExcerptCount").GetInt32() >= 1);
        });

        var evidencePlan = rootElement.GetProperty("sourceEvidencePlan");
        Assert.Equal("/api/events", evidencePlan.GetProperty("eventEndpoint").GetString());
        Assert.Equal("/api/memory/proposals", evidencePlan.GetProperty("proposalEndpoint").GetString());
        Assert.Equal("/api/memory/context", evidencePlan.GetProperty("contextEndpoint").GetString());
        Assert.Equal("/api/memory/context/feedback", evidencePlan.GetProperty("feedbackEndpoint").GetString());
        Assert.Contains(
            "sourceContentSha256",
            evidencePlan.GetProperty("requiredEventFields").EnumerateArray().Select(element => element.GetString()).ToArray());

        var reviewCadence = rootElement.GetProperty("reviewCadence").EnumerateArray().ToArray();
        Assert.Contains(reviewCadence, item => item.GetProperty("cadence").GetString() == "weekly"
            && item.GetProperty("ownerRoleId").GetString() == "knowledge_steward");
        Assert.Contains(reviewCadence, item => item.GetProperty("cadence").GetString() == "weekly"
            && item.GetProperty("ownerRoleId").GetString() == "security_professional");
        Assert.Contains(reviewCadence, item => item.GetProperty("cadence").GetString() == "after roadmap or backlog change"
            && item.GetProperty("ownerRoleId").GetString() == "product_owner");

        Assert.Equal(
            "scripts/seed-production-memory-boundary.sh",
            rootElement.GetProperty("commands").GetProperty("boundarySeed").GetString());
        Assert.Equal(
            "scripts/weekly-admin-review-workflow.sh --scope-type project --scope-id 9f8e7d6c-5b4a-4321-9123-abcdef123002",
            rootElement.GetProperty("commands").GetProperty("weeklyReview").GetString());

        Assert.Empty(rootElement.GetProperty("errors").EnumerateArray());
    }

    private static readonly string[] ExpectedRoleIds =
    [
        "product_owner",
        "cto",
        "security_professional",
        "it_manager",
        "developer",
        "tester_qa",
        "release_manager",
        "knowledge_steward"
    ];

    private static readonly string[] ExpectedMemoryTypes =
    [
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
    ];

    private static async Task<ScriptResult> RunScriptAsync(string root, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment.Remove("MEMORYSYSTEM_API_KEY");
        startInfo.Environment.Remove("MEMORYSYSTEM_OPERATOR_API_KEY");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start project onboarding runbook script.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ScriptResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
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

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ScriptResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
