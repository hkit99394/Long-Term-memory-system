namespace MemorySystem.UnitTests;

public sealed class ConsoleLoginFlowTests
{
    [Fact]
    public void Console_login_assets_and_consoles_enforce_browser_login_flow_contract()
    {
        var root = FindRepositoryRoot();
        var loginScript = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "auth", "login.js"));
        var logoutScript = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "auth", "logout.js"));
        var loginStyles = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "auth", "login.css"));
        var adminScript = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var adminHtml = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "index.html"));
        var reviewScript = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "reviews", "review-dashboard.js"));
        var reviewHtml = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "reviews", "index.html"));

        Assert.Contains("/api/auth/console/password", loginScript, StringComparison.Ordinal);
        Assert.Contains("payload.credential || submittedCredential", loginScript, StringComparison.Ordinal);
        Assert.Contains("sessionStorage.setItem(credentialKey", loginScript, StringComparison.Ordinal);
        Assert.Contains("status.focus()", loginScript, StringComparison.Ordinal);
        Assert.Contains("sessionStorage.removeItem(credentialKey)", logoutScript, StringComparison.Ordinal);
        Assert.Contains("window.location.replace(`/auth/login?returnUrl=", logoutScript, StringComparison.Ordinal);
        Assert.Contains(".login-shell", loginStyles, StringComparison.Ordinal);
        Assert.Contains(".environment-badge", loginStyles, StringComparison.Ordinal);

        foreach (var script in new[] { adminScript, reviewScript })
        {
            Assert.Contains("response.status === 401", script, StringComparison.Ordinal);
            Assert.DoesNotContain("response.status === 401 || response.status === 403", script, StringComparison.Ordinal);
            Assert.Contains("sessionStorage.removeItem(credentialStorageKey)", script, StringComparison.Ordinal);
            Assert.Contains("sessionStorage.removeItem(credentialKindStorageKey)", script, StringComparison.Ordinal);
            Assert.Contains("readStoredCredential()", script, StringComparison.Ordinal);
            Assert.Contains("usesBearerCredential", script, StringComparison.Ordinal);
            Assert.Contains("/auth/logout?returnUrl=", script, StringComparison.Ordinal);
            Assert.Contains("/auth/login?returnUrl=", script, StringComparison.Ordinal);
        }

        Assert.Contains("const consoleReturnUrl = \"/admin/\";", adminScript, StringComparison.Ordinal);
        Assert.Contains("const consoleReturnUrl = \"/reviews/\";", reviewScript, StringComparison.Ordinal);
        Assert.Contains("data-1p-ignore", adminHtml, StringComparison.Ordinal);
        Assert.Contains("data-1p-ignore", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("class=\"connection-internal\" hidden", adminHtml, StringComparison.Ordinal);
        Assert.Contains("class=\"credential-state connection-internal\"", adminHtml, StringComparison.Ordinal);
        Assert.Contains("class=\"connection-internal\" hidden", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"/auth/logout?returnUrl=/admin/\"", adminHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"/auth/logout?returnUrl=/reviews/\"", reviewHtml, StringComparison.Ordinal);
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
