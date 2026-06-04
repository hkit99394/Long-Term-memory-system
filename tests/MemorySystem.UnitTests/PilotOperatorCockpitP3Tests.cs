using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class PilotOperatorCockpitP3Tests
{
    [Fact]
    public void Pilot_operator_cockpit_is_documented_scripted_and_status_driven()
    {
        var root = FindRepositoryRoot();
        var cockpit = File.ReadAllText(Path.Combine(root, "docs", "pilot-operator-cockpit-p3.md"));
        var status = File.ReadAllText(Path.Combine(root, "docs", "external-pilot-readiness-status.json"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var html = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "index.html"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var source = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin-console.ts"));
        var endpoints = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminConsoleEndpointExtensions.cs"));
        var apiProject = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "MemorySystem.Api.csproj"));

        Assert.Contains("# Pilot Operator Cockpit P3", cockpit, StringComparison.Ordinal);
        Assert.Contains("GET /api/admin/pilot/readiness", cockpit, StringComparison.Ordinal);
        Assert.Contains("externalInviteApproved: true", cockpit, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(status);
        var gates = document.RootElement.GetProperty("gates")
            .EnumerateArray()
            .ToDictionary(gate => gate.GetProperty("id").GetString()!);

        Assert.Equal("done", gates["EPR-07"].GetProperty("status").GetString());
        Assert.Equal("P3", gates["EPR-07"].GetProperty("priority").GetString());
        Assert.Equal("done", gates["EPR-04"].GetProperty("status").GetString());

        Assert.Contains("| EPR-07 | P3 | Done | Add pilot operator cockpit.", backlog, StringComparison.Ordinal);
        Assert.Contains("[Pilot Operator Cockpit P3](pilot-operator-cockpit-p3.md)", index, StringComparison.Ordinal);
        Assert.Contains("pilot-operator-cockpit-p3.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("""<option value="pilot">Pilot</option>""", html, StringComparison.Ordinal);
        Assert.Contains("/api/admin/pilot/readiness", script, StringComparison.Ordinal);
        Assert.Contains("renderPilotDetail", source, StringComparison.Ordinal);
        Assert.Contains("/api/admin/pilot/readiness", endpoints, StringComparison.Ordinal);
        Assert.Contains("external-pilot-readiness-status.json", apiProject, StringComparison.Ordinal);
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
