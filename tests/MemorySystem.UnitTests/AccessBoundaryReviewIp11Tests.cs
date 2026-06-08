using System.Diagnostics;
using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class AccessBoundaryReviewIp11Tests
{
    [Fact]
    public void Ip11_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "access-boundary-review-ip11.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var runbook = File.ReadAllText(Path.Combine(root, "docs", "project-memory-runbook.md"));
        var weeklyWorkflow = File.ReadAllText(Path.Combine(root, "docs", "weekly-admin-review-workflow-ip09.md"));
        var permissionDrift = File.ReadAllText(Path.Combine(root, "docs", "permission-drift-report-gc02.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var acceptedFindings = File.ReadAllText(Path.Combine(root, "docs", "access-boundary-accepted-findings.json"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "access-boundary-review.sh"));
        var weeklyScript = File.ReadAllText(Path.Combine(root, "scripts", "weekly-admin-review-workflow.sh"));

        Assert.Contains("| IP-11 | P1 | Done | Security Professional | Access Boundary Review |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Access Boundary Review IP-11", contract, StringComparison.Ordinal);
        Assert.Contains("[Access Boundary Review IP-11](access-boundary-review-ip11.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/access-boundary-review-ip11.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("docs/access-boundary-accepted-findings.json", folderStructure, StringComparison.Ordinal);
        Assert.Contains("scripts/access-boundary-review.sh", runbook, StringComparison.Ordinal);
        Assert.Contains("scripts/access-boundary-review.sh", weeklyWorkflow, StringComparison.Ordinal);
        Assert.Contains("scripts/access-boundary-review.sh", permissionDrift, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/access-boundary-review.sh", testing, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "/api/admin/access/permission-drift",
            "organizationMemberships",
            "projectMemberships",
            "roleAssignments",
            "namespaceGrants",
            "serviceAccounts",
            "serviceCredentials",
            "identityBindings",
            "break_glass_key_review",
            "oidc_identity_binding_review",
            "audit_export_evidence",
            "accepted_finding_owner_review",
            "acceptedFindingSource",
            "acceptedFindings",
            "unacceptedFindings"
        })
        {
            Assert.Contains(required, script, StringComparison.Ordinal);
        }

        foreach (var required in new[]
        {
            "memberships",
            "role assignments",
            "namespace grants",
            "service accounts",
            "service credentials",
            "OIDC identity bindings",
            "break-glass",
            "permission drift",
            "accepted-finding"
        })
        {
            Assert.Contains(required, contract, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("\"kind\": \"memorysystem.access_boundary_accepted_findings\"", acceptedFindings, StringComparison.Ordinal);
        Assert.Contains("\"payloadSafe\": true", acceptedFindings, StringComparison.Ordinal);
        Assert.Contains("\"rawSourcePayloadsIncluded\": false", acceptedFindings, StringComparison.Ordinal);
        Assert.Contains("accessBoundaryReviewCommand", weeklyScript, StringComparison.Ordinal);
        Assert.Contains("access_boundary_permission_drift", weeklyScript, StringComparison.Ordinal);
        Assert.Contains("/api/admin/access/permission-drift", weeklyScript, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Access_boundary_review_dry_run_is_payload_safe_and_complete()
    {
        var root = FindRepositoryRoot();

        var syntax = await RunScriptAsync(root, ["-n", "scripts/access-boundary-review.sh"]);
        Assert.Equal(0, syntax.ExitCode);

        var weeklySyntax = await RunScriptAsync(root, ["-n", "scripts/weekly-admin-review-workflow.sh"]);
        Assert.Equal(0, weeklySyntax.ExitCode);

        var help = await RunScriptAsync(root, ["scripts/access-boundary-review.sh", "--help"]);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("payload-safe access boundary review queue", help.StandardOutput, StringComparison.Ordinal);

        var dryRun = await RunScriptAsync(root, ["scripts/access-boundary-review.sh", "--dry-run"]);
        Assert.Equal(0, dryRun.ExitCode);

        using var document = JsonDocument.Parse(dryRun.StandardOutput);
        var rootElement = document.RootElement;

        Assert.Equal("dry_run", rootElement.GetProperty("status").GetString());
        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.Equal("project", rootElement.GetProperty("targetScopeType").GetString());
        Assert.Equal("9f8e7d6c-5b4a-4321-9123-abcdef123002", rootElement.GetProperty("targetScopeId").GetString());
        Assert.Equal("/project/9f8e7d6c-5b4a-4321-9123-abcdef123002", rootElement.GetProperty("namespacePrefix").GetString());

        var request = rootElement.GetProperty("permissionDriftRequest");
        Assert.Equal("POST", request.GetProperty("method").GetString());
        Assert.Equal("/api/admin/access/permission-drift", request.GetProperty("endpoint").GetString());
        Assert.Equal(90, request.GetProperty("body").GetProperty("staleAfterDays").GetInt32());
        Assert.Equal(20, request.GetProperty("body").GetProperty("maxPreviewPrincipals").GetInt32());

        var checks = rootElement.GetProperty("checks").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("membership_audit", checks);
        Assert.Contains("role_assignment_audit", checks);
        Assert.Contains("namespace_grant_audit", checks);
        Assert.Contains("service_account_owner_review", checks);
        Assert.Contains("service_credential_rotation_review", checks);
        Assert.Contains("oidc_identity_binding_review", checks);
        Assert.Contains("break_glass_key_review", checks);
        Assert.Contains("permission_drift_findings", checks);
        Assert.Contains("accepted_finding_owner_review", checks);
        Assert.Contains("audit_export_evidence", checks);

        var acceptedFindingSource = rootElement.GetProperty("acceptedFindingSource");
        Assert.Equal("docs/access-boundary-accepted-findings.json", acceptedFindingSource.GetProperty("path").GetString());
        Assert.True(acceptedFindingSource.GetProperty("exists").GetBoolean());
        Assert.Equal(1, acceptedFindingSource.GetProperty("schemaVersion").GetInt32());
        Assert.True(acceptedFindingSource.GetProperty("ruleCount").GetInt32() >= 4);

        var endpoints = rootElement.GetProperty("endpoints").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("/api/admin/access/permission-drift", endpoints);
        Assert.Contains("/api/admin/access/effective-preview", endpoints);
        Assert.Contains("/api/admin/audit-exports", endpoints);
        Assert.Contains("/api/auth/console/oidc-token", endpoints);
        Assert.Contains("/api/auth/console/break-glass-key", endpoints);

        var reportSections = rootElement.GetProperty("reportSections").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("identityBindings", reportSections);
        Assert.Contains("serviceAccounts", reportSections);
        Assert.Contains("serviceCredentials", reportSections);
        Assert.Contains("organizationMemberships", reportSections);
        Assert.Contains("projectMemberships", reportSections);
        Assert.Contains("roleAssignments", reportSections);
        Assert.Contains("namespaceGrants", reportSections);
        Assert.Contains("effectiveAccessPreviews", reportSections);
        Assert.Contains("findings", reportSections);
        Assert.Contains("acceptedFindings", reportSections);

        var manualCheckIds = rootElement.GetProperty("manualChecks").EnumerateArray()
            .Select(element => element.GetProperty("id").GetString())
            .ToArray();
        Assert.Contains("break_glass_key_owner", manualCheckIds);
        Assert.Contains("break_glass_key_scope", manualCheckIds);
        Assert.Contains("break_glass_key_rotation", manualCheckIds);

        var weeklyDryRun = await RunScriptAsync(root, ["scripts/weekly-admin-review-workflow.sh", "--dry-run"]);
        Assert.Equal(0, weeklyDryRun.ExitCode);

        using var weeklyDocument = JsonDocument.Parse(weeklyDryRun.StandardOutput);
        var weeklyRoot = weeklyDocument.RootElement;
        Assert.Equal(
            "scripts/access-boundary-review.sh --scope-type project --scope-id 9f8e7d6c-5b4a-4321-9123-abcdef123002",
            weeklyRoot.GetProperty("accessBoundaryReviewCommand").GetString());
        Assert.Contains(
            "access_boundary_permission_drift",
            weeklyRoot.GetProperty("checks").EnumerateArray().Select(element => element.GetString()).ToArray());
    }

    [Fact]
    public void Accepted_access_boundary_findings_are_payload_safe_and_narrow()
    {
        var root = FindRepositoryRoot();
        var acceptedFindingsPath = Path.Combine(root, "docs", "access-boundary-accepted-findings.json");

        using var document = JsonDocument.Parse(File.ReadAllText(acceptedFindingsPath));
        var rootElement = document.RootElement;

        Assert.Equal("memorysystem.access_boundary_accepted_findings", rootElement.GetProperty("kind").GetString());
        Assert.Equal(1, rootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.Equal("project", rootElement.GetProperty("targetScopeType").GetString());
        Assert.Equal("9f8e7d6c-5b4a-4321-9123-abcdef123002", rootElement.GetProperty("targetScopeId").GetString());

        var forbiddenRootNamespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "/global",
            "/org",
            "/project",
            "/user",
            "/role",
            "/agent",
            "/session"
        };

        var rules = rootElement.GetProperty("rules").EnumerateArray().ToArray();
        Assert.True(rules.Length >= 4);

        foreach (var rule in rules)
        {
            Assert.Equal("active", rule.GetProperty("status").GetString());
            Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("ownerRole").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("acceptedByRole").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("acceptedReason").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("cleanupAction").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("reviewDue").GetString()));

            if (rule.TryGetProperty("namespacePrefixes", out var namespacePrefixes))
            {
                foreach (var namespacePrefix in namespacePrefixes.EnumerateArray())
                {
                    Assert.DoesNotContain(namespacePrefix.GetString()!, forbiddenRootNamespaces);
                }
            }
        }

        Assert.Contains(
            rules,
            rule => rule.GetProperty("id").GetString() == "ip11-canonical-project-bootstrap-namespace-admin");
    }

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
            ?? throw new InvalidOperationException("Could not start access boundary review.");

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

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed record ScriptResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
