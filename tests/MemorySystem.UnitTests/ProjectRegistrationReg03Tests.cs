namespace MemorySystem.UnitTests;

public sealed class ProjectRegistrationReg03Tests
{
    [Fact]
    public void Reg03_admin_project_registration_wizard_is_documented_bundled_and_payload_safe()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var registrationPlan = File.ReadAllText(Path.Combine(root, "docs", "project-registration-ux-plan.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var html = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "index.html"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var registrationPanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "registration-panel.ts"));
        var buildScript = File.ReadAllText(Path.Combine(root, "tools", "ui", "build-review-dashboard.mjs"));

        Assert.Contains("| REG-03 | P0 | Done | Designer + Developer | Admin Wizard MVP |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| REG-03 | P0 | Done | Build admin Project Registration wizard MVP.", backlog, StringComparison.Ordinal);
        Assert.Contains("Status: REG-01 through REG-06 implemented.", registrationPlan, StringComparison.Ordinal);
        Assert.Contains("## REG-03 Admin Wizard MVP", registrationPlan, StringComparison.Ordinal);
        Assert.Contains("ProjectRegistrationReg03Tests", testing, StringComparison.Ordinal);

        Assert.Contains("""<option value="registration">Project Registration</option>""", html, StringComparison.Ordinal);
        Assert.Contains("src/admin/registration-panel.ts", buildScript, StringComparison.Ordinal);
        Assert.Contains("tools/ui/src/admin/registration-panel.ts", script, StringComparison.Ordinal);

        foreach (var step in new[]
                 {
                     "Scope",
                     "Owners/Roles",
                     "Grants",
                     "Seed Docs",
                     "Preflight",
                     "Register",
                     "Closeout"
                 })
        {
            Assert.Contains(step, registrationPanel, StringComparison.Ordinal);
            Assert.Contains(step, script, StringComparison.Ordinal);
        }

        foreach (var fragment in new[]
                 {
                     "/api/admin/access/effective-preview",
                     "/api/admin/projects/register",
                     "Idempotency-Key",
                     "accessPreviewReportId",
                     "payloadSafe",
                     "rawSourcePayloadsIncluded",
                     "sourceContentSha256",
                     "registrationValidationItems",
                     "registrationAccessPlanFingerprint",
                     "preview is stale after access-plan edits",
                     "registrationDraftChanged",
                     "registrationReadOnlyField(`registration-grant-namespace-${index}`, \"Namespace\", grant.namespacePrefix)",
                     "roleIsKnown(source.sourceOwnerRoleId)",
                     "registrationSeedReadinessItems",
                     "\"read\", \"write\", \"review\""
                 })
        {
            Assert.Contains(fragment, registrationPanel, StringComparison.Ordinal);
            Assert.Contains(fragment, script, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("[\"read\", \"write\", \"review\", \"admin\"]", registrationPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"custom\"", registrationPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("registrationTextField(`registration-grant-namespace-${index}`", registrationPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"\", ...registrationRoleOptions()]", registrationPanel, StringComparison.Ordinal);
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
