using System.Diagnostics;
using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ProjectRegistrationUxPlanTests
{
    [Fact]
    public void Project_registration_plan_is_documented_and_planned()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var registrationPlan = File.ReadAllText(Path.Combine(root, "docs", "project-registration-ux-plan.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var onboarding = File.ReadAllText(Path.Combine(root, "docs", "project-onboarding-runbook-ip16.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));

        Assert.Contains("| REG-01 | P0 | Done | Product Owner + Security Professional + Knowledge Steward + Developer | Project Registration Specification And Dry-Run Contract |", productPlan, StringComparison.Ordinal);
        Assert.Contains("## Project Registration UX", backlog, StringComparison.Ordinal);
        Assert.Contains("| REG-01 | P0 | Done | Define project registration specification and dry-run contract. |", backlog, StringComparison.Ordinal);
        Assert.Contains("| REG-06 | P1 | Done | Measure registration success. |", backlog, StringComparison.Ordinal);
        Assert.Contains("[Project Registration UX Plan](project-registration-ux-plan.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/project-registration-ux-plan.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("[Project Registration UX Plan](project-registration-ux-plan.md)", onboarding, StringComparison.Ordinal);
        Assert.Contains("project registration plan", testing, StringComparison.OrdinalIgnoreCase);

        foreach (var targetDate in new[]
        {
            "2026-06-12",
            "2026-06-19",
            "2026-06-26",
            "2026-07-03",
            "2026-07-10",
            "2026-07-17"
        })
        {
            Assert.Contains(targetDate, registrationPlan, StringComparison.Ordinal);
        }

        foreach (var required in new[]
        {
            "organizationId",
            "projectId",
            "Product Owner principal",
            "Knowledge Steward principal",
            "custom project role definitions",
            "namespace grant preset",
            "source hashes",
            "registrationContract",
            "registrationValidation",
            "least-privilege",
            "bootstrap-admin",
            "Project Details, People & Responsibilities, Seed Evidence, Access Rules, Review & Validate, Register, Finish Setup",
            "REG-UX-01 Beginner-Friendly Registration Wizard",
            "zero root namespace grants",
            "effective access"
        })
        {
            Assert.Contains(required, registrationPlan, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Reg01_registration_dry_run_contract_is_payload_safe_and_complete()
    {
        var root = FindRepositoryRoot();

        var result = await RunScriptAsync(
            root,
            [
                "scripts/project-onboarding-runbook.sh",
                "--dry-run",
                "--product-owner-principal-id",
                "11111111-1111-4111-8111-111111111111",
                "--knowledge-steward-principal-id",
                "22222222-2222-4222-8222-222222222222",
                "--security-ops-principal-id",
                "33333333-3333-4333-8333-333333333333",
                "--custom-role",
                "research_lead=Research Lead",
                "--deferred-role",
                "cfo"
            ]);

        Assert.Equal(0, result.ExitCode);

        using var document = JsonDocument.Parse(result.StandardOutput);
        var rootElement = document.RootElement;

        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());

        var registrationContract = rootElement.GetProperty("registrationContract");
        Assert.Equal("REG-01", registrationContract.GetProperty("contractId").GetString());
        Assert.Equal("ready", registrationContract.GetProperty("status").GetString());
        Assert.Equal("2026-06-12", registrationContract.GetProperty("targetDate").GetString());

        var requiredFields = registrationContract.GetProperty("requiredFields").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("organizationId", requiredFields);
        Assert.Contains("productOwnerPrincipalId", requiredFields);
        Assert.Contains("customRoleDefinitions", requiredFields);
        Assert.Contains("namespaceGrantPreset", requiredFields);
        Assert.Contains("seedDocuments", requiredFields);
        Assert.Contains("reviewCadence", requiredFields);
        Assert.Contains("adminAuditEvidenceIdForAnyAdminGrant", registrationContract.GetProperty("auditEvidencePlan").GetProperty("requiredEvidence").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray());

        var grantPresets = registrationContract.GetProperty("grantPresets").EnumerateArray().ToArray();
        Assert.Contains(grantPresets, preset => preset.GetProperty("id").GetString() == "least_privilege_default"
            && preset.GetProperty("default").GetBoolean());
        Assert.Contains(grantPresets, preset => preset.GetProperty("id").GetString() == "bootstrap_admin_exception");

        var ownerAssignments = registrationContract.GetProperty("ownerAssignments").EnumerateArray().ToArray();
        Assert.Contains(ownerAssignments, assignment => assignment.GetProperty("principalLabel").GetString() == "product_owner"
            && assignment.GetProperty("principalId").GetString() == "11111111-1111-4111-8111-111111111111");
        Assert.Contains(ownerAssignments, assignment => assignment.GetProperty("principalLabel").GetString() == "knowledge_steward"
            && assignment.GetProperty("principalId").GetString() == "22222222-2222-4222-8222-222222222222");
        Assert.Contains(ownerAssignments, assignment => assignment.GetProperty("principalLabel").GetString() == "security_ops"
            && assignment.GetProperty("principalId").GetString() == "33333333-3333-4333-8333-333333333333");

        var grantMatrix = registrationContract.GetProperty("namespaceGrantMatrix").EnumerateArray().ToArray();
        Assert.Contains(grantMatrix, grant => grant.GetProperty("namespace").GetString() == "/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/goals"
            && grant.GetProperty("permission").GetString() == "write");
        Assert.DoesNotContain(grantMatrix, grant => grant.GetProperty("namespace").GetString() == "/project");
        Assert.DoesNotContain(grantMatrix, grant => grant.GetProperty("permission").GetString() == "admin");

        var sourceDocChecks = registrationContract.GetProperty("sourceDocChecks");
        Assert.Equal(8, sourceDocChecks.GetProperty("documentCount").GetInt32());
        Assert.Equal(0, sourceDocChecks.GetProperty("missingDocumentCount").GetInt32());
        Assert.Equal(100, sourceDocChecks.GetProperty("sha256CoveragePercent").GetInt32());
        Assert.False(sourceDocChecks.GetProperty("rawSourcePayloadsIncluded").GetBoolean());

        Assert.Equal(
            "/api/admin/access/effective-preview",
            registrationContract.GetProperty("effectiveAccessPreviewPlan").GetProperty("endpoint").GetString());
        Assert.Equal(
            "/api/admin/audit-exports",
            registrationContract.GetProperty("auditEvidencePlan").GetProperty("endpoint").GetString());

        var operations = registrationContract.GetProperty("plannedOperations").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("upsert_project", operations);
        Assert.Contains("upsert_namespace_grants_from_preset", operations);
        Assert.Contains("run_effective_access_preview", operations);
        Assert.Contains("record_context_feedback", operations);

        var closeoutCriteria = registrationContract.GetProperty("closeoutCriteria").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("access-boundary review has zero unaccepted high-severity findings", closeoutCriteria);
        Assert.Contains("memory context feedback is recorded", closeoutCriteria);

        var validation = rootElement.GetProperty("registrationValidation");
        Assert.Equal("ready", validation.GetProperty("status").GetString());
        Assert.Empty(validation.GetProperty("blockingValidationErrors").EnumerateArray());
        var validationRules = validation.GetProperty("validationRules").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("root_namespace_grants_are_forbidden", validationRules);
        Assert.Contains("effective_access_preview_is_required_before_commit", validationRules);
    }

    [Fact]
    public async Task Reg01_registration_dry_run_rejects_non_guid_scope_ids()
    {
        var root = FindRepositoryRoot();

        var result = await RunScriptAsync(
            root,
            [
                "scripts/project-onboarding-runbook.sh",
                "--dry-run",
                "--organization-id",
                "aaaaaaaaaaaaaaaa",
                "--project-id",
                "bbbbbbbbbbbbbbbb",
                "--product-owner-principal-id",
                "11111111-1111-4111-8111-111111111111",
                "--knowledge-steward-principal-id",
                "22222222-2222-4222-8222-222222222222",
                "--security-ops-principal-id",
                "33333333-3333-4333-8333-333333333333"
            ]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("project id must be a UUID.", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reg01_bootstrap_admin_dry_run_requires_audit_evidence_id()
    {
        var root = FindRepositoryRoot();

        var result = await RunScriptAsync(
            root,
            [
                "scripts/project-onboarding-runbook.sh",
                "--dry-run",
                "--namespace-grant-preset",
                "bootstrap-admin",
                "--product-owner-principal-id",
                "11111111-1111-4111-8111-111111111111",
                "--knowledge-steward-principal-id",
                "22222222-2222-4222-8222-222222222222",
                "--security-ops-principal-id",
                "33333333-3333-4333-8333-333333333333",
                "--admin-accepted-finding-owner",
                "security_professional",
                "--admin-accepted-reason",
                "Temporary bounded bootstrap.",
                "--admin-review-due",
                "2099-12-31",
                "--admin-cleanup-action",
                "Remove admin grants after bootstrap."
            ]);

        Assert.Equal(0, result.ExitCode);

        using var document = JsonDocument.Parse(result.StandardOutput);
        var validation = document.RootElement.GetProperty("registrationValidation");
        Assert.Equal("needs_input", validation.GetProperty("status").GetString());
        var blockingErrors = validation.GetProperty("blockingValidationErrors").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("adminAuditEvidenceId is required when admin grants are requested.", blockingErrors);
    }

    [Fact]
    public void Reg01_registration_wizard_enforces_required_owners_and_canonical_namespaces()
    {
        var root = FindRepositoryRoot();
        var registrationPanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "registration-panel.ts"));

        Assert.Contains("const registrationRequiredOwnerRoleIds = [", registrationPanel, StringComparison.Ordinal);
        Assert.Contains("registrationMissingRequiredOwnerRoles", registrationPanel, StringComparison.Ordinal);
        foreach (var roleId in new[] { "product_owner", "knowledge_steward", "security_professional" })
        {
            Assert.Contains($"\"{roleId}\"", registrationPanel, StringComparison.Ordinal);
        }

        foreach (var namespaceArea in new[] { "goals", "facts", "decisions", "rationale", "risks", "release-evidence" })
        {
            Assert.Contains($"\"{namespaceArea}\"", registrationPanel, StringComparison.Ordinal);
        }

        Assert.Contains("namespaceArea: \"goals\"", registrationPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"requirements\"", registrationPanel, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed record ScriptResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
